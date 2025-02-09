// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.UseLeftJoinInsteadOfGroupJoinPatternAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public class UseLeftJoinInsteadOfGroupJoinPatternTests
    {
        //=> VerifyCS.VerifyAnalyzerAsync("""
        [Fact]
        public Task Foo()
            => VerifyCSharpDiagnosticAsync("""
                 using System.Linq;

                 class Program
                 {
                     public static void Foo()
                     {
                         Student[] students = new[] { new Student { FirstName = "John", LastName = "Doe", DepartmentId = 1 } };
                         Department[] departments = new[] { new Department { Id = 1, Name = "Math" } };
                         
                         var query = {|CA1874:students
                             .GroupJoin(
                                 departments,
                                 student => student.DepartmentId,
                                 department => department.Id,
                                 (student, departmentList) => new { student, subgroup = departmentList })
                             .SelectMany(
                                 joinedSet => joinedSet.subgroup.DefaultIfEmpty(),
                                 (student, department) => new
                                 {
                                     student.student.FirstName,
                                     student.student.LastName,
                                     Department = department.Name
                                 })|};
                     }
                 }
                 
                 class Student
                 {
                     public string FirstName { get; set; }
                     public string LastName { get; set; }
                     public int DepartmentId { get; set; }
                 }
                 
                 class Department
                 {
                     public int Id { get; set; }
                     public string Name { get; set; }
                 }
                 """);

        private static async Task VerifyCSharpDiagnosticAsync([StringSyntax($"{LanguageNames.CSharp}-Test")] string source, CodeAnalysis.CSharp.LanguageVersion? languageVersion = null)
        {
            await new VerifyCS.Test
            {
                TestCode = source,
                FixedCode = source,
                // TODO: Need .NET 10 for LeftJoin!
                // ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
                LanguageVersion = languageVersion ?? CodeAnalysis.CSharp.LanguageVersion.CSharp10
            }.RunAsync();
        }
    }
}

