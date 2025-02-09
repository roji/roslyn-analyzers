// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Microsoft.NetCore.Analyzers.Performance
{
    using ULJIGJAnalyzer = UseLeftJoinInsteadOfGroupJoinPatternAnalyzer;

    public class UseLeftJoinInsteadOfGroupJoinPatternFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(ULJIGJAnalyzer.Id);

        public override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            // throw new System.NotImplementedException();
            return Task.CompletedTask;
        }
    }
}

