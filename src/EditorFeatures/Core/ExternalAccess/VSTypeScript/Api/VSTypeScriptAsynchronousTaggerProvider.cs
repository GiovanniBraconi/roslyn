﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Editor.Tagging;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.CodeAnalysis.Workspaces;
using Microsoft.VisualStudio.Text.Tagging;

namespace Microsoft.CodeAnalysis.ExternalAccess.VSTypeScript.Api;

internal abstract class VSTypeScriptAsynchronousTaggerProvider<TTag> : AsynchronousViewTaggerProvider<TTag>
    where TTag : ITag
{
    [Obsolete("Use constructor that takes ITextBufferVisibilityTracker.  Use `[Import(AllowDefault = true)] ITextBufferVisibilityTracker? visibilityTracker`")]
    protected VSTypeScriptAsynchronousTaggerProvider(
        IThreadingContext threadingContext,
        IAsynchronousOperationListenerProvider asyncListenerProvider,
        VSTypeScriptGlobalOptions globalOptions)
        : this(
            threadingContext,
            globalOptions,
            visibilityTracker: null,
            asyncListenerProvider)
    {
    }

    [Obsolete("Use constructor that takes a single TaggerHost")]
    protected VSTypeScriptAsynchronousTaggerProvider(
        IThreadingContext threadingContext,
        VSTypeScriptGlobalOptions globalOptions,
        ITextBufferVisibilityTracker? visibilityTracker,
        IAsynchronousOperationListenerProvider asyncListenerProvider)
        : base(new TaggerHost(threadingContext, globalOptions.Service, visibilityTracker, asyncListenerProvider), FeatureAttribute.Classification)
    {
    }

    protected VSTypeScriptAsynchronousTaggerProvider(TaggerHost taggerHost)
        : base(taggerHost, FeatureAttribute.Classification)
    {
    }
}
