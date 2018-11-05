using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.FixDeclaredType;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.Diagnostics;
using Microsoft.CodeAnalysis.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.FixDeclaredType
{
    public class FixDeclaredTypeTests : AbstractCSharpDiagnosticProviderBasedUserDiagnosticTest
    {
        internal override (DiagnosticAnalyzer, CodeFixProvider) CreateDiagnosticProviderAndFixer(Workspace workspace)
            => (null, new CSharpFixDeclaredTypeCodeFixProvider());

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsFixDeclaredType)]
        public async Task VoidMethod()
        {
            await TestInRegularAndScriptAsync(
@"
public class C
{
    public void M()
    {
        [|return|] 0;
    }
}",
@"
public class C
{
    public int M()
    {
        return 0;
    }
}");
        }
    }
}
