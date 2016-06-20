// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Desktop.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Desktop.CSharp.Analyzers
{
    /// <summary>
    /// CA1301: Avoid duplicate accelerators
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpAvoidDuplicateAcceleratorsAnalyzer : AvoidDuplicateAcceleratorsAnalyzer
    {
    }
}