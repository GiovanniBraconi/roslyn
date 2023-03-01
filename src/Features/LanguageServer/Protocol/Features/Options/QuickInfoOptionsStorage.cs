﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.CodeAnalysis.Options;

namespace Microsoft.CodeAnalysis.QuickInfo
{
    internal static class QuickInfoOptionsStorage
    {
        public static QuickInfoOptions GetQuickInfoOptions(this IGlobalOptionService globalOptions, string language)
          => new()
          {
              ShowRemarksInQuickInfo = globalOptions.GetOption(ShowRemarksInQuickInfo, language),
              IncludeNavigationHintsInQuickInfo = globalOptions.GetOption(IncludeNavigationHintsInQuickInfo),
          };

        private static readonly OptionGroup s_quickInfoGroup = new(
            FeaturesResources.Symbol_Description,
            int.MaxValue,
            null,
            FeaturesResources.ResourceManager.GetString(nameof(FeaturesResources.Symbol_Description), new CultureInfo("en")));

        public static readonly PerLanguageOption2<bool> ShowRemarksInQuickInfo = new(
            "dotnet_show_remarks", QuickInfoOptions.Default.ShowRemarksInQuickInfo, group: s_quickInfoGroup);

        public static readonly Option2<bool> IncludeNavigationHintsInQuickInfo = new(
            "dotnet_include_navigation_hints", QuickInfoOptions.Default.IncludeNavigationHintsInQuickInfo);
    }
}
