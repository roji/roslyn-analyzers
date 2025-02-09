// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;

namespace Microsoft.NetCore.Analyzers.Performance
{
    /// <summary>
    /// TODO CA1874: Prefer the StringComparison method overloads to perform case-insensitive string comparisons.
    /// </summary>
    // TODO:
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    // [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UseLeftJoinInsteadOfGroupJoinPatternAnalyzer : DiagnosticAnalyzer
    {
        internal const string Id = "CA1874";

        private const string LeftJoinText = "LeftJoin";
        private const string SelectManyText = "SelectMany";
        private const string GroupJoinText = "GroupJoin";
        private const string DefaultIfEmptyText = "DefaultIfEmpty";

        // TODO
        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            Id,
            CreateLocalizableResourceString(nameof(PreferLengthCountIsEmptyOverAnyTitle)),
            CreateLocalizableResourceString(nameof(PreferIsEmptyOverAnyMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(PreferLengthCountIsEmptyOverAnyDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            var typeProvider = WellKnownTypeProvider.GetOrCreate(context.Compilation);

            // TODO: Also queryable

            // var iEnumerable = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsIEnumerable);
            // var iEnumerableOfT = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericIEnumerable1);
            // var linqExpressionType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemLinqExpressionsExpression1);

            if (typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemLinqEnumerable) is ITypeSymbol enumerable
                // TODO: .NET 10 is needed for LeftJoin method
                // && enumerable.GetMembers(LeftJoinText)
                    // .OfType<IMethodSymbol>()
                    // .FirstOrDefault(m => m.IsExtensionMethod && m.Parameters.Length == 5) is IMethodSymbol leftJoinMethod)
                && enumerable.GetMembers(SelectManyText)
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault(m => m.IsExtensionMethod && m.Parameters.Length == 3) is IMethodSymbol selectManyMethod
                && enumerable.GetMembers(GroupJoinText)
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault(m => m.IsExtensionMethod && m.Parameters.Length == 5) is IMethodSymbol groupJoinMethod
                && enumerable.GetMembers(DefaultIfEmptyText)
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault(m => m.IsExtensionMethod && m.Parameters.Length == 1) is IMethodSymbol defaultIfEmptyMethod)
            {
                context.RegisterOperationAction(
                    c => OnInvocationAnalysis(c, /*leftJoinMethod*/ null!, selectManyMethod, groupJoinMethod,
                        defaultIfEmptyMethod),
                    OperationKind.Invocation);
            }
        }

        private static void OnInvocationAnalysis(
            OperationAnalysisContext context,
            IMethodSymbol leftJoinMethod,
            IMethodSymbol selectManyMethod,
            IMethodSymbol groupJoinMethod,
            IMethodSymbol defaultIfEmptyMethod)
        {
            // Pattern match:
            // students
            //     .GroupJoin(
            //         departments,
            //         student => student.DepartmentID,
            //         department => department.ID,
            //         (student, departmentList) => new { student, subgroup = departmentList })
            //     .SelectMany(
            //         joinedSet => joinedSet.subgroup.DefaultIfEmpty(),
            //         (student, department) => new
            //         {
            //             student.student.FirstName,
            //             student.student.LastName,
            //             Department = department.Name
            //         });

            var selectManyInvocation = (IInvocationOperation)context.Operation;

            // TODO: Arguments[0] (extension, non-extension?)
            if (IsIncorrectMethod(selectManyInvocation, selectManyMethod)
                || selectManyInvocation.Arguments[0].Value is not IInvocationOperation groupJoinInvocation
                || IsIncorrectMethod(groupJoinInvocation, groupJoinMethod))
            {
                return;
            }

            // Pattern-match the GroupJoin's result selector, matching only for anonymous object creation
            if (groupJoinInvocation.Arguments[4].Value is not IDelegateCreationOperation
                {
                    Target: IAnonymousFunctionOperation
                    {
                        Symbol.Parameters: [_, IParameterSymbol groupJoinInnerGroupParameter],
                        Body.Operations: [IReturnOperation { ReturnedValue: IAnonymousObjectCreationOperation groupJoinResultSelectorAnonymousObject }]
                    }
                })
            {
                return;
            }

            // Find the anonymous object initializer which references the GroupJoin grouping parameter and extract
            // the property referencing it.
            var assignment = groupJoinResultSelectorAnonymousObject.Initializers
                .OfType<ISimpleAssignmentOperation>()
                .FirstOrDefault(i => i is { Value: IParameterReferenceOperation parameterReference }
                                     && parameterReference.Parameter.Equals(groupJoinInnerGroupParameter));
            if (assignment?.Target is not IPropertyReferenceOperation { Property: var anonymousObjectGroupProperty })
            {
                return;
            }

            // We have the anonymous object property that represents the grouping.
            // Now pattern-match the SelectMany collection selector, matching only when it selects the grouping property.
            if (selectManyInvocation.Arguments[1].Value is not IDelegateCreationOperation
                {
                    Target: IAnonymousFunctionOperation
                    {
                        Symbol.Parameters: [IParameterSymbol selectManyCollectionSelectorParameter],
                        Body.Operations: [IReturnOperation { ReturnedValue: IInvocationOperation defaultIfEmptyInvocation }]
                    }
                }
                || IsIncorrectMethod(defaultIfEmptyInvocation, defaultIfEmptyMethod))
            {
                return;
            }

            // Check that the SelectMany collection selector is a reference to the GroupJoin's result selector's grouping property
            if (defaultIfEmptyInvocation.Arguments[0].Value is not IPropertyReferenceOperation { Property: var defaultIfEmptyProperty }
                || !defaultIfEmptyProperty.Equals(anonymousObjectGroupProperty))
            {
                return;
            }

            context.ReportDiagnostic(selectManyInvocation.CreateDiagnostic(Rule));

            static bool IsIncorrectMethod(IInvocationOperation invocation, IMethodSymbol otherMethod)
            {
                var originalMethod = invocation.TargetMethod.OriginalDefinition;
                if (originalMethod.MethodKind is MethodKind.ReducedExtension)
                {
                    originalMethod = originalMethod.ReducedFrom!;
                }

                return !originalMethod.Equals(otherMethod, SymbolEqualityComparer.Default);
            }
        }
    }
}