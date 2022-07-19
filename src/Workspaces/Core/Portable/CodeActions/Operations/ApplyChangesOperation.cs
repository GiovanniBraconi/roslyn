﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Shared.Utilities;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CodeActions
{
    /// <summary>
    /// A <see cref="CodeActionOperation"/> for applying solution changes to a workspace.
    /// <see cref="CodeAction.GetOperationsAsync(CancellationToken)"/> may return at most one
    /// <see cref="ApplyChangesOperation"/>. Hosts may provide custom handling for 
    /// <see cref="ApplyChangesOperation"/>s, but if a <see cref="CodeAction"/> requires custom
    /// host behavior not supported by a single <see cref="ApplyChangesOperation"/>, then instead:
    /// <list type="bullet">
    /// <description><text>Implement a custom <see cref="CodeAction"/> and <see cref="CodeActionOperation"/>s</text></description>
    /// <description><text>Do not return any <see cref="ApplyChangesOperation"/> from <see cref="CodeAction.GetOperationsAsync(CancellationToken)"/></text></description>
    /// <description><text>Directly apply any workspace edits</text></description>
    /// <description><text>Handle any custom host behavior</text></description>
    /// <description><text>Produce a preview for <see cref="CodeAction.GetPreviewOperationsAsync(CancellationToken)"/> 
    ///   by creating a custom <see cref="PreviewOperation"/> or returning a single <see cref="ApplyChangesOperation"/>
    ///   to use the built-in preview mechanism</text></description>
    /// </list>
    /// </summary>
    public sealed class ApplyChangesOperation : CodeActionOperation
    {
        public Solution ChangedSolution { get; }

        public ApplyChangesOperation(Solution changedSolution)
            => ChangedSolution = changedSolution ?? throw new ArgumentNullException(nameof(changedSolution));

        internal override bool ApplyDuringTests => true;

        public override void Apply(Workspace workspace, CancellationToken cancellationToken)
            => workspace.TryApplyChanges(ChangedSolution, new ProgressTracker());

        internal sealed override Task<bool> TryApplyAsync(
            Workspace workspace, Solution originalSolution, IProgressTracker progressTracker, CancellationToken cancellationToken)
        {
            var currentSolution = workspace.CurrentSolution;

            // if there was no intermediary edit, just apply the change fully.
            if (ChangedSolution.WorkspaceVersion == currentSolution.WorkspaceVersion)
                return Task.FromResult(workspace.TryApplyChanges(ChangedSolution, progressTracker));

            // Otherwise, we need to see what changes were actually made and see if we can apply them.  The general rules are:
            //
            // 1. we only support text changes when doing merges.  Any other changes to projects/documents are not
            //    supported because it's very unclear what impact they may have wrt other workspace updates that have
            //    already happened.
            //
            // 2. For text changes, we only support it if the current text of the document we're changing itself has not
            //    changed. This means we can merge in edits if there were changes to unrelated files, but not if there
            //    are changes to the current file.  We could consider relaxing this in the future, esp. if we make use
            //    of some sort of text-merging-library to handle this.  However, the user would then have to handle diff
            //    markers being inserted into their code that they then have to handle.

            var solutionChanges = this.ChangedSolution.GetChanges(originalSolution);

            if (solutionChanges.GetAddedProjects().Count() > 0 ||
                solutionChanges.GetAddedAnalyzerReferences().Count() > 0 ||
                solutionChanges.GetRemovedProjects().Count() > 0 ||
                solutionChanges.GetRemovedAnalyzerReferences().Count() > 0)
            {
                return SpecializedTasks.False;
            }

            // Take the actual current solution the workspace is pointing to and fork it with just the text changes the
            // code action wanted to make.  Then apply that fork back into the workspace.
            var forkedSolution = currentSolution;

            foreach (var changedProject in solutionChanges.GetProjectChanges())
            {
                // We only support text changes.  If we see any other changes to this project, bail out immediately.
                if (changedProject.GetAddedAdditionalDocuments().Count() > 0 ||
                    changedProject.GetAddedAnalyzerConfigDocuments().Count() > 0 ||
                    changedProject.GetAddedAnalyzerReferences().Count() > 0 ||
                    changedProject.GetAddedDocuments().Count() > 0 ||
                    changedProject.GetAddedMetadataReferences().Count() > 0 ||
                    changedProject.GetAddedProjectReferences().Count() > 0 ||
                    changedProject.GetRemovedAdditionalDocuments().Count() > 0 ||
                    changedProject.GetRemovedAnalyzerConfigDocuments().Count() > 0 ||
                    changedProject.GetRemovedAnalyzerReferences().Count() > 0 ||
                    changedProject.GetRemovedDocuments().Count() > 0 ||
                    changedProject.GetRemovedMetadataReferences().Count() > 0 ||
                    changedProject.GetRemovedProjectReferences().Count() > 0)
                {
                    return SpecializedTasks.False;
                }

                if (!ProcessDocuments(changedProject, changedProject.GetChangedDocuments(), static (s, i) => s.GetRequiredDocument(i), static (s, i, t) => s.WithDocumentText(i, t)) ||
                    !ProcessDocuments(changedProject, changedProject.GetChangedAdditionalDocuments(), static (s, i) => s.GetRequiredAdditionalDocument(i), static (s, i, t) => s.WithAdditionalDocumentText(i, t)) ||
                    !ProcessDocuments(changedProject, changedProject.GetChangedAnalyzerConfigDocuments(), static (s, i) => s.GetRequiredAnalyzerConfigDocument(i), static (s, i, t) => s.WithAnalyzerConfigDocumentText(i, t)))
                {
                    return SpecializedTasks.False;
                }
            }

            return Task.FromResult(workspace.TryApplyChanges(forkedSolution, progressTracker));

            bool ProcessDocuments(
                ProjectChanges changedProject,
                IEnumerable<DocumentId> changedDocuments,
                Func<Solution, DocumentId, TextDocument> getDocument,
                Func<Solution, DocumentId, SourceText, Solution> withDocumentText)
            {
                var sawChangedDocument = false;

                foreach (var documentId in changedDocuments)
                {
                    sawChangedDocument = true;

                    var originalDocument = getDocument(changedProject.OldProject.Solution, documentId);
                    var changedDocument = getDocument(changedProject.NewProject.Solution, documentId);

                    // it has to be a text change the operation wants to make.  If the operation is making some other
                    // sort of change, we can't merge this operation in.
                    if (!changedDocument.HasTextChanged(originalDocument, ignoreUnchangeableDocument: false))
                        return false;

                    // If the document has gone away, we definitely cannot apply a text change to it.
                    var currentDocument = getDocument(currentSolution, documentId);
                    if (currentDocument is null)
                        return false;

                    // If the file contents changed in the current workspace, then we can't apply this change to it.
                    // Note: we could potentially try to do a 3-way merge in the future, including handling conflicts
                    // with that.  For now though, we'll leave that out of scope.
                    if (originalDocument.HasTextChanged(currentDocument, ignoreUnchangeableDocument: false))
                        return false;

                    forkedSolution = withDocumentText(forkedSolution, documentId, changedDocument.GetTextSynchronously(cancellationToken));
                }

                return sawChangedDocument;
            }
        }
    }
}
