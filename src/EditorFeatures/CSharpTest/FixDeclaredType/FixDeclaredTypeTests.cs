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
        public async Task VoidMethodToInt()
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

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsFixDeclaredType)]
        public async Task VoidMethodToObject()
        {
            await TestInRegularAndScriptAsync(
@"
public class C
{
    public void M()
    {
        [|return|] new object();
    }
}",
@"
public class C
{
    public object M()
    {
        return new object();
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsFixDeclaredType)]
        public async Task IntMethodToObject()
        {
            await TestInRegularAndScriptAsync(
@"
public class C
{
    public int M()
    {
        return [|new object()|];
    }
}",
@"
public class C
{
    public object M()
    {
        return new object();
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsFixDeclaredType)]
        public async Task IntMethodToObjectForAnonymousReturnType()
        {
            await TestInRegularAndScriptAsync(
@"
public class C
{
    public int M()
    {
        return [|new { }|];
    }
}",
@"
public class C
{
    public object M()
    {
        return new { };
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsFixDeclaredType)]
        public async Task PreservesTrivia()
        {
            await TestInRegularAndScriptAsync(
@"
public class C
{
    public /*LeadingTrivia*/void/*TrailingTrivia*/ M()
    {
        [|return|] 0;
    }
}",
@"
public class C
{
    public /*LeadingTrivia*/int/*TrailingTrivia*/ M()
    {
        return 0;
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsFixDeclaredType)]
        public async Task MissingForDynamicMethod()
        {
            await TestMissingInRegularAndScriptAsync(
@"
public class C
{
    public dynamic M()
    {
        [|return 0|];
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsFixDeclaredType)]
        public async Task AsyncVoidMethodToTaskOfInt()
        {
            await TestInRegularAndScriptAsync(
@"
public class C
{
    public async void M()
    {
        [|return|] 0;
    }
}",
@"
public class C
{
    public async Task<int> M()
    {
        return 0;
    }
}");
        }
    }
}
