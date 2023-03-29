﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Editor.Shared;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Internal.Log;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Remote;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.UnifiedSuggestions;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.Implementation.Suggestions
{
    internal partial class SuggestedActionsSourceProvider
    {
        private partial class SuggestedActionsSource : IAsyncSuggestedActionsSource
        {
            public async Task GetSuggestedActionsAsync(
                ISuggestedActionCategorySet requestedActionCategories,
                SnapshotSpan range,
                ImmutableArray<ISuggestedActionSetCollector> collectors,
                CancellationToken cancellationToken)
            {
                AssertIsForeground();
                using var _ = ArrayBuilder<ISuggestedActionSetCollector>.GetInstance(out var completedCollectors);
                try
                {
                    await GetSuggestedActionsWorkerAsync(
                        requestedActionCategories, range, collectors, completedCollectors, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    // Always ensure that all the collectors are marked as complete so we don't hang the UI.
                    foreach (var collector in collectors)
                    {
                        if (!completedCollectors.Contains(collector))
                            collector.Complete();
                    }
                }
            }

            private async Task GetSuggestedActionsWorkerAsync(
                ISuggestedActionCategorySet requestedActionCategories,
                SnapshotSpan range,
                ImmutableArray<ISuggestedActionSetCollector> collectors,
                ArrayBuilder<ISuggestedActionSetCollector> completedCollectors,
                CancellationToken cancellationToken)
            {
                AssertIsForeground();
                using var state = SourceState.TryAddReference();
                if (state is null)
                    return;

                var workspace = state.Target.Workspace;
                if (workspace is null)
                    return;

                var selection = TryGetCodeRefactoringSelection(state, range);
                await workspace.Services.GetRequiredService<IWorkspaceStatusService>().WaitUntilFullyLoadedAsync(cancellationToken).ConfigureAwait(false);

                using (Logger.LogBlock(FunctionId.SuggestedActions_GetSuggestedActionsAsync, cancellationToken))
                {
                    var document = range.Snapshot.GetOpenTextDocumentInCurrentContextWithChanges();
                    if (document is null)
                        return;

                    // Create a single keep-alive session as we process each lightbulb priority group.  We want to
                    // ensure that all calls to OOP will reuse the same solution-snapshot on the oop side (including
                    // reusing all the same computed compilations that may have been computed on that side.  This is
                    // especially important as we are sending disparate requests for diagnostics, and we do not want the
                    // individual diagnostic requests to redo all the work to run source generators, create skeletons,
                    // etc.
                    using var _1 = RemoteKeepAliveSession.Create(document.Project.Solution, _listener);

                    // Keep track of how many actions we've put in the lightbulb at each priority level.  We do
                    // this as each priority level will both sort and inline actions.  However, we don't want to
                    // inline actions at each priority if it's going to make the total number of actions too high.
                    // This does mean we might inline actions from a higher priority group, and then disable 
                    // inlining for lower pri groups.  However, intuitively, that is what we want.  More important
                    // items should be pushed higher up, and less important items shouldn't take up that much space.
                    var currentActionCount = 0;

                    var pendingActionSets = new MultiDictionary<CodeActionRequestPriority, SuggestedActionSet>();

                    // Collectors are in priority order.  So just walk them from highest to lowest.
                    foreach (var collector in collectors)
                    {
                        if (TryGetPriority(collector.Priority) is CodeActionRequestPriority priority)
                        {
                            var allSets = GetCodeFixesAndRefactoringsAsync(
                                state, requestedActionCategories, document,
                                range, selection,
                                addOperationScope: _ => null,
                                priority,
                                currentActionCount, cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false);

                            await foreach (var set in allSets)
                            {
                                if (ShouldPendActionSet(priority, set.Priority, out var pendTo))
                                {
                                    pendingActionSets.Add(pendTo, set);
                                }
                                else
                                {
                                    currentActionCount += set.Actions.Count();
                                    collector.Add(set);
                                }
                            }

                            foreach (var set in pendingActionSets[priority])
                            {
                                currentActionCount += set.Actions.Count();
                                collector.Add(set);
                            }
                        }

                        // Ensure we always complete the collector even if we didn't add any items to it.
                        // This ensures that we unblock the UI from displaying all the results for that
                        // priority class.
                        collector.Complete();
                        completedCollectors.Add(collector);
                    }
                }

                return;

                static bool ShouldPendActionSet(
                    CodeActionRequestPriority requestPriority,
                    SuggestedActionSetPriority setPriority,
                    out CodeActionRequestPriority pendTo)
                {
                    var actualPriorityDeterminedBySet = setPriority switch
                    {
                        SuggestedActionSetPriority.None => CodeActionRequestPriority.Lowest,
                        SuggestedActionSetPriority.Low => CodeActionRequestPriority.Low,
                        SuggestedActionSetPriority.Medium => CodeActionRequestPriority.Normal,
                        SuggestedActionSetPriority.High =>  CodeActionRequestPriority.High,
                        _ => throw ExceptionUtilities.UnexpectedValue(setPriority),
                    };

                    if (actualPriorityDeterminedBySet < requestPriority)
                    {
                        // We got a result that is lower pri than what we asked for.  This should go in a later bucket
                        // when the lightbulb gets around to it.
                        pendTo = actualPriorityDeterminedBySet;
                        return true;
                    }

                    // We got a result that either goes with our current priority class, or is even higher than what
                    // we asked for.  We def want to add this now.
                    pendTo = default;
                    return false;
                }
            }

            private async IAsyncEnumerable<SuggestedActionSet> GetCodeFixesAndRefactoringsAsync(
                ReferenceCountedDisposable<State> state,
                ISuggestedActionCategorySet requestedActionCategories,
                TextDocument document,
                SnapshotSpan range,
                TextSpan? selection,
                Func<string, IDisposable?> addOperationScope,
                CodeActionRequestPriority priority,
                int currentActionCount,
                [EnumeratorCancellation] CancellationToken cancellationToken)
            {
                var workspace = document.Project.Solution.Workspace;
                var supportsFeatureService = workspace.Services.GetRequiredService<ITextBufferSupportsFeatureService>();

                var options = GlobalOptions.GetCodeActionOptionsProvider();

                var fixesTask = GetCodeFixesAsync(
                    state, supportsFeatureService, requestedActionCategories, workspace, document, range,
                    addOperationScope, priority, options, isBlocking: false, cancellationToken);
                var refactoringsTask = GetRefactoringsAsync(
                    state, supportsFeatureService, requestedActionCategories, GlobalOptions, workspace, document, selection,
                    addOperationScope, priority, options, isBlocking: false, cancellationToken);

                await Task.WhenAll(fixesTask, refactoringsTask).ConfigureAwait(false);

                var fixes = await fixesTask.ConfigureAwait(false);
                var refactorings = await refactoringsTask.ConfigureAwait(false);
                foreach (var set in ConvertToSuggestedActionSets(state, selection, fixes, refactorings, currentActionCount))
                    yield return set;
            }
        }
    }
}
