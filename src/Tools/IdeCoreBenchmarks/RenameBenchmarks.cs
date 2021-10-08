﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Options;

namespace IdeCoreBenchmarks
{
    [MemoryDiagnoser]
    public class RenameBenchmarks
    {
        private readonly int _iterationCount = 5;

        private Document _document;
        private Solution _solution;
        private ISymbol _symbol;
        private OptionSet _options;

        [GlobalSetup]
        public void GlobalSetup()
        {
            var roslynRoot = Environment.GetEnvironmentVariable(Program.RoslynRootPathEnvVariableName);
            var csFilePath = Path.Combine(roslynRoot, @"src\Compilers\CSharp\Portable\Generated\BoundNodes.xml.Generated.cs");

            if (!File.Exists(csFilePath))
            {
                throw new ArgumentException();
            }

            var projectId = ProjectId.CreateNewId();
            var documentId = DocumentId.CreateNewId(projectId);

            _solution = new AdhocWorkspace().CurrentSolution
                .AddProject(projectId, "ProjectName", "AssemblyName", LanguageNames.CSharp)
                .AddDocument(documentId, "DocumentName", File.ReadAllText(csFilePath));

            var document = _solution.GetDocument(documentId);
            var root = document.GetSyntaxRootAsync(CancellationToken.None).Result.WithAdditionalAnnotations(Formatter.Annotation);
            _solution = _solution.WithDocumentSyntaxRoot(documentId, root);
            _document = _solution.GetDocument(documentId);
            var project = _solution.Projects.First();
            var compilation = project.GetCompilationAsync().Result;
            _symbol = compilation.GetTypeByMetadataName("Microsoft.CodeAnalysis.CSharp.BoundKind");
        }

        [Benchmark]
        public void RenameNodes()
        {
            _ = Microsoft.CodeAnalysis.Rename.Renamer.RenameSymbolAsync(_solution, _symbol, "NewName", _options);
        }
    }
}
