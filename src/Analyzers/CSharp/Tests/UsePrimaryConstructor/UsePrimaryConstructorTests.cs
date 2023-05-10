﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.UsePrimaryConstructor;
using Microsoft.CodeAnalysis.Editor.UnitTests.CodeActions;
using Microsoft.CodeAnalysis.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.Analyzers.UnitTests.UsePrimaryConstructor;

using VerifyCS = CSharpCodeFixVerifier<
    CSharpUsePrimaryConstructorDiagnosticAnalyzer,
    CSharpUsePrimaryConstructorCodeFixProvider>;

[Trait(Traits.Feature, Traits.Features.CodeActionsUsePrimaryConstructor)]
public partial class UsePrimaryConstructorTests
{
    [Fact]
    public async Task TestInCSharp12()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                class C
                {
                    public [|C|](int i)
                    {
                    }
                }
                """,
            FixedCode = """
                class C(int i)
                {
                }
                """,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestNotInCSharp11()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                class C
                {
                    public C(int i)
                    {
                    }
                }
                """,
            LanguageVersion = LanguageVersion.CSharp11,
        }.RunAsync();
    }

    [Fact]
    public async Task TestNotWithNonPublicConstructor()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                class C
                {
                    private C(int i)
                    {
                    }
                }
                """,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestStruct()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                struct C
                {
                    public [|C|](int i)
                    {
                    }
                }
                """,
            FixedCode = """
                struct C(int i)
                {
                }
                """,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestNotWithUnchainedConstructor()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                class C
                {
                    public C(int i)
                    {
                    }

                    public C()
                    {
                    }
                }
                """,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestWithThisChainedConstructor()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                class C
                {
                    public [|C|](int i)
                    {
                    }

                    public C() : this(0)
                    {
                    }
                }
                """,
            FixedCode = """
                class C(int i)
                {
                    public C() : this(0)
                    {
                    }
                }
                """,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestWithBaseChainedConstructor1()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                class B(int i)
                {
                }

                class C : B
                {
                    public [|C|](int i) : base(i)
                    {
                    }

                    public C() : this(0)
                    {
                    }
                }
                """,
            FixedCode = """
                class B(int i)
                {
                }

                class C(int i) : B(i)
                {
                    public C() : this(0)
                    {
                    }
                }
                """,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestWithBaseChainedConstructor2()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                class B(int i)
                {
                }

                class C : B
                {
                    public [|C|](int i) : base(i * i)
                    {
                    }

                    public C() : this(0)
                    {
                    }
                }
                """,
            FixedCode = """
                class B(int i)
                {
                }

                class C(int i) : B(i * i)
                {
                    public C() : this(0)
                    {
                    }
                }
                """,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestNotWithBadExpressionBody()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                class C
                {
                    public C(int i)
                        => System.Console.WriteLine(i);
                }
                """,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestNotWithBadBlockBody()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                class C
                {
                    public C(int i)
                    {
                        System.Console.WriteLine(i);
                    }
                }
                """,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestWithExpressionBodyAssignmentToField1()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                class C
                {
                    private int i;

                    public [|C|](int i)
                        => this.i = i;
                }
                """,
            FixedCode = """
                class C(int i)
                {
                    private int i = i;
                }
                """,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestWithExpressionBodyAssignmentToField2()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                class C
                {
                    private int i;

                    public [|C|](int j)
                        => this.i = j;
                }
                """,
            FixedCode = """
                class C(int j)
                {
                    private int i = j;
                }
                """,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestWithExpressionBodyAssignmentToField3()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                class C
                {
                    private int i;

                    public [|C|](int j)
                        => i = j;
                }
                """,
            FixedCode = """
                class C(int j)
                {
                    private int i = j;
                }
                """,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestNotWithAssignmentToBaseField1()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                class B
                {
                    public int i;
                }

                class C : B
                {
                    public C(int i)
                        => this.i = i;
                }
                """,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestNotWithAssignmentToBaseField2()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                class B
                {
                    public int i;
                }

                class C : B
                {
                    public C(int i)
                        => base.i = i;
                }
                """,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestNotWithCompoundAssignmentToField()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                class C
                {
                    public int i;

                    public C(int i)
                        => this.i += i;
                }
                """,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestNotWithTwoWritesToTheSameMember()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                class C
                {
                    public int i;

                    public C(int i, int j)
                    {
                        this.i = i;
                        this.i = j;
                    }
                }
                """,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestWithComplexRightSide1()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                class C
                {
                    private int i;

                    public [|C|](int i)
                        => this.i = i * 2;
                }
                """,
            FixedCode = """
                class C(int i)
                {
                    private int i = i * 2;
                }
                """,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestBlockWithMultipleAssignments1()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                class C
                {
                    public int i;
                    public int j;

                    public [|C|](int i, int j)
                    {
                        this.i = i;
                        this.j = j;
                    }
                }
                """,
            FixedCode = """
                class C(int i, int j)
                {
                    public int i = i;
                    public int j = j;
                }
                """,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestRemoveMembers1()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                class C
                {
                    private int i;
                    private int j;

                    public [|C|](int i, int j)
                    {
                        this.i = i;
                        this.j = j;
                    }
                }
                """,
            FixedCode = """
                class C(int i, int j)
                {
                }
                """,
            CodeActionIndex = 1,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestDoNotRemovePublicMembers1()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                class C
                {
                    public int i;
                    public int j;

                    public [|C|](int i, int j)
                    {
                        this.i = i;
                        this.j = j;
                    }
                }
                """,
            CodeActionIndex = 1,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestRemoveMembersUpdateReferences1()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                using System;
                class C
                {
                    private int i;
                    private int j;

                    public [|C|](int i, int j)
                    {
                        this.i = i;
                        this.j = j;
                    }

                    void M()
                    {
                        Console.WriteLine(this.i + this.j);
                    }
                }
                """,
            FixedCode = """
                using System;
                class C(int i, int j)
                {
                    void M()
                    {
                        Console.WriteLine(i + j);
                    }
                }
                """,
            CodeActionIndex = 1,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestRemoveMembersUpdateReferencesWithRename1()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                using System;
                class C
                {
                    private int _i;
                    private int _j;

                    public [|C|](int i, int j)
                    {
                        _i = i;
                        _j = j;
                    }

                    void M()
                    {
                        Console.WriteLine(_i + _j);
                    }
                }
                """,
            FixedCode = """
                using System;
                class C(int i, int j)
                {
                    void M()
                    {
                        Console.WriteLine(i + j);
                    }
                }
                """,
            CodeActionIndex = 1,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestRemoveMembersOnlyPrivateMembers()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                using System;
                class C
                {
                    private int _i;
                    public int _j;

                    public [|C|](int i, int j)
                    {
                        _i = i;
                        _j = j;
                    }

                    void M()
                    {
                        Console.WriteLine(_i + _j);
                    }
                }
                """,
            FixedCode = """
                using System;
                class C(int i, int j)
                {
                    public int _j = j;

                    void M()
                    {
                        Console.WriteLine(i + _j);
                    }
                }
                """,
            CodeActionIndex = 1,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestRemoveMembersOnlyMembersWithoutAttributes()
    {
        await new VerifyCS.Test
        {
            TestCode = """
            using System;
            class C
            {
                private int _i;
                [CLSCompliant(true)]
                private int _j;

                public [|C|](int i, int j)
                {
                    _i = i;
                    _j = j;
                }

                void M()
                {
                    Console.WriteLine(_i + _j);
                }
            }
            """,
            FixedCode = """
            using System;
            class C(int i, int j)
            {
                [CLSCompliant(true)]
                private int _j = j;

                void M()
                {
                    Console.WriteLine(i + _j);
                }
            }
            """,
            CodeActionIndex = 1,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestRemoveMembersAccessedOffThis()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                using System;
                class C
                {
                    private int _i;
                    private int _j;

                    public [|C|](int i, int j)
                    {
                        _i = i;
                        _j = j;
                    }

                    void M(C c)
                    {
                        Console.WriteLine(_i);
                        Console.WriteLine(_j == c._j);
                    }
                }
                """,
            FixedCode = """
                using System;
                class C(int i, int j)
                {
                    private int _j = j;

                    void M(C c)
                    {
                        Console.WriteLine(i);
                        Console.WriteLine(_j == c._j);
                    }
                }
                """,
            CodeActionIndex = 1,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestNotWhenRightSideReferencesThis1()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                class C
                {
                    private int x;

                    public C(int i)
                    {
                        x = M(i);
                    }

                    int M(int y) => y;
                }
                """,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestNotWhenRightSideReferencesThis2()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                class C
                {
                    private int x;

                    public C(int i)
                    {
                        x = this.M(i);
                    }

                    int M(int y) => y;
                }
                """,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestWhenRightSideDoesNotReferenceThis()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                class C
                {
                    private int x;

                    public [|C|](int i)
                    {
                        x = M(i);
                    }

                    static int M(int y) => y;
                }
                """,
            FixedCode = """
                class C(int i)
                {
                    private int x = M(i);

                    static int M(int y) => y;
                }
                """,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="i"></param>
    void M(int i)
    {

    }

    [Fact]
    public async Task TestMoveConstructorDocCommentWhenNothingOnType_SingleLine_1()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                namespace N
                {
                    class C
                    {
                        private int i;

                        /// <summary>Doc comment on single line</summary>
                        /// <param name="i">Doc about i single line</param>
                        public [|C|](int i)
                        {
                            this.i = i;
                        }
                    }
                }
                """,
            FixedCode = """
                namespace N
                {
                    /// <summary>Doc comment on single line</summary>
                    /// <param name="i">Doc about i single line</param>
                    class C(int i)
                    {
                        private int i = i;
                    }
                }
                """,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestMoveConstructorDocCommentWhenNothingOnType_SingleLine_2()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                class C
                {
                    private int i;

                    /// <summary>Doc comment on single line</summary>
                    /// <param name="i">Doc about i single line</param>
                    public [|C|](int i)
                    {
                        this.i = i;
                    }
                }
                """,
            FixedCode = """
                /// <summary>Doc comment on single line</summary>
                /// <param name="i">Doc about i single line</param>
                class C(int i)
                {
                    private int i = i;
                }
                """,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestMoveConstructorDocCommentWhenNothingOnType_MultiLine_1()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                namespace N
                {
                    class C
                    {
                        private int i;

                        /// <summary>
                        /// Doc comment
                        /// On multiple lines
                        /// </summary>
                        /// <param name="i">
                        /// Doc about i
                        /// on multiple lines
                        /// </param>
                        public [|C|](int i)
                        {
                            this.i = i;
                        }
                    }
                }
                """,
            FixedCode = """
                namespace N
                {
                    /// <summary>
                    /// Doc comment
                    /// On multiple lines
                    /// </summary>
                    /// <param name="i">
                    /// Doc about i
                    /// on multiple lines
                    /// </param>
                    class C(int i)
                    {
                        private int i = i;
                    }
                }
                """,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestMoveConstructorDocCommentWhenNothingOnType_MultiLine_2()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                namespace N
                {
                    class C
                    {
                        private int i;

                        /// <summary>
                        /// Doc comment
                        /// On multiple lines</summary>
                        /// <param name="i">
                        /// Doc about i
                        /// on multiple lines</param>
                        public [|C|](int i)
                        {
                            this.i = i;
                        }
                    }
                }
                """,
            FixedCode = """
                namespace N
                {
                    /// <summary>
                    /// Doc comment
                    /// On multiple lines</summary>
                    /// <param name="i">
                    /// Doc about i
                    /// on multiple lines</param>
                    class C(int i)
                    {
                        private int i = i;
                    }
                }
                """,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestMoveConstructorDocCommentWhenNothingOnType_MultiLine_3()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                namespace N
                {
                    class C
                    {
                        private int i;

                        /// <summary>Doc comment
                        /// On multiple lines</summary>
                        /// <param name="i">Doc about i
                        /// on multiple lines</param>
                        public [|C|](int i)
                        {
                            this.i = i;
                        }
                    }
                }
                """,
            FixedCode = """
                namespace N
                {
                    /// <summary>Doc comment
                    /// On multiple lines</summary>
                    /// <param name="i">Doc about i
                    /// on multiple lines</param>
                    class C(int i)
                    {
                        private int i = i;
                    }
                }
                """,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestMoveConstructorParamDocCommentsIntoTypeDocComments1()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                namespace N
                {
                    /// <summary>
                    /// Existing doc comment
                    /// </summary>
                    class C
                    {
                        private int i;
                        private int j;

                        /// <summary>Constructor comment
                        /// On multiple lines</summary>
                        /// <param name="i">Doc about i</param>
                        /// <param name="i">Doc about j</param>
                        public [|C|](int i, int j)
                        {
                            this.i = i;
                            this.j = j;
                        }
                    }
                }
                """,
            FixedCode = """
                namespace N
                {
                    /// <summary>
                    /// Existing doc comment
                    /// </summary>
                    /// <remarks>Constructor comment
                    /// On multiple lines</remarks>
                    /// <param name="i">Doc about i</param>
                    /// <param name="i">Doc about j</param>
                    class C(int i, int j)
                    {
                        private int i = i;
                        private int j = j;
                    }
                }
                """,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }

    [Fact]
    public async Task TestMoveConstructorParamDocCommentsIntoTypeDocComments2()
    {
        await new VerifyCS.Test
        {
            TestCode = """
                namespace N
                {
                    /// <summary>Existing doc comment</summary>
                    class C
                    {
                        private int i;
                        private int j;

                        /// <summary>Constructor comment</summary>
                        /// <param name="i">Doc about
                        /// i</param>
                        /// <param name="i">Doc about
                        /// j</param>
                        public [|C|](int i, int j)
                        {
                            this.i = i;
                            this.j = j;
                        }
                    }
                }
                """,
            FixedCode = """
                namespace N
                {
                    /// <summary>Existing doc comment</summary>
                    /// <remarks>Constructor comment</remarks>
                    /// <param name="i">Doc about
                    /// i</param>
                    /// <param name="i">Doc about
                    /// j</param>
                    class C(int i, int j)
                    {
                        private int i = i;
                        private int j = j;
                    }
                }
                """,
            LanguageVersion = LanguageVersion.Preview,
        }.RunAsync();
    }
}
