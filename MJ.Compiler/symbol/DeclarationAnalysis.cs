using System;
using System.Collections.Generic;

using mj.compiler.main;
using mj.compiler.resources;
using mj.compiler.tree;
using mj.compiler.utils;

using static mj.compiler.symbol.Scope;
using static mj.compiler.symbol.Symbol;

namespace mj.compiler.symbol
{
    /// <summary>
    /// Constructs symbols for declarations, like funcs and func parameters.
    /// Does not go into func bodies.
    /// </summary>
    /// <para>
    /// 
    /// </para>
    public class DeclarationAnalysis
    {
        private static readonly Context.Key<DeclarationAnalysis> CONTEXT_KEY = new Context.Key<DeclarationAnalysis>();

        public static DeclarationAnalysis instance(Context ctx)
        {
            if (ctx.tryGet(CONTEXT_KEY, out var instance)) {
                return instance;
            }
            return new DeclarationAnalysis(ctx);
        }

        private readonly EnterTypes enterTypes;
        private readonly EnterMembers enterMembers;

        public DeclarationAnalysis(Context ctx)
        {
            ctx.put(CONTEXT_KEY, this);

            Symtab symtab = Symtab.instance(ctx);
            Check  check  = Check.instance(ctx);
            Log    log    = Log.instance(ctx);
            
            enterTypes = new EnterTypes(symtab, check, log);
            enterMembers = new EnterMembers(symtab, check, log);
        }

        public IList<CompilationUnit> main(IList<CompilationUnit> trees)
        {
            if (trees.Count == 0) {
                return CollectionUtils.emptyList<CompilationUnit>();
            }
            enterTypes.main(trees);
            enterMembers.main(trees);
            return trees;
        }

        class EnterTypes : AstVisitor<Object, WritableScope>
        {
            private Symtab symtab;
            private Check check;
            private Log log;

            public EnterTypes(Symtab symtab, Check check, Log log)
            {
                this.symtab = symtab;
                this.check = check;
                this.log = log;
            }

            public void main(IList<CompilationUnit> trees)
            {
                WritableScope topScope = symtab.topLevelSymbol.topScope;

                foreach (CompilationUnit tree in trees) {
                    SourceFile prevSource = log.useSource(tree.sourceFile);
                    try {
                        scan(tree, topScope);
                    } finally {
                        log.useSource(prevSource);
                    }
                }
            }

            public override object visitCompilationUnit(CompilationUnit compilationUnit, WritableScope s)
            {
                for (var i = 0; i < compilationUnit.declarations.Count; i++) {
                    Tree decl = compilationUnit.declarations[i];
                    if (decl.Tag == Tag.STRUCT_DEF) {
                        visitStructDef((StructDef)decl, s);
                    }
                }
                return null;
            }

            public override object visitStructDef(StructDef structDef, WritableScope enclScope)
            {
                StructSymbol ssym = makeStructSymbol(structDef, enclScope.owner);

                if (check.checkUnique(structDef.Pos, ssym, enclScope)) {
                    enclScope.enter(ssym);
                }
                return null;
            }

            private StructSymbol makeStructSymbol(StructDef structDef, Symbol owner)
            {
                StructSymbol ssym = new StructSymbol(structDef.name, owner, null);
                ssym.type = new StructType(ssym);

                structDef.symbol = ssym;

                return ssym;
            }
        }

        class EnterMembers : AstVisitor<Object, WritableScope>
        {
            private Symtab symtab;
            private Check check;
            private Log log;

            public bool mainFuncFound = false;

            public EnterMembers(Symtab symtab, Check check, Log log)
            {
                this.symtab = symtab;
                this.check = check;
                this.log = log;
            }

            public void main(IList<CompilationUnit> trees)
            {
                WritableScope topScope = symtab.topLevelSymbol.topScope;

                foreach (CompilationUnit tree in trees) {
                    SourceFile prevSource = log.useSource(tree.sourceFile);
                    try {
                        scan(tree, topScope);
                    } finally {
                        log.useSource(prevSource);
                    }
                }
                
                if (!mainFuncFound) {
                    log.globalError(messages.mainFunctionNotDefined);
                }
            }

            public override object visitCompilationUnit(CompilationUnit compilationUnit, WritableScope s)
                => scan(compilationUnit.declarations, s);

            public override object visitStructDef(StructDef structDef, WritableScope s)
            {
                WritableScope membersScope = WritableScope.create(structDef.symbol);
                structDef.symbol.membersScope = membersScope;
                return scan(structDef.members, new CompoundScope(membersScope, s));
            }

            /**
             * INFO: This is for fields. Another non-visitor routine is for function parameters.
             */
            public override object visitVarDef(VariableDeclaration varDef, WritableScope compoundScope)
            {
                WritableScope membersScope = ((StructSymbol)compoundScope.owner).membersScope;
                // TODO: Should be NON_RECURSIVE
                Symbol sym = membersScope.findFirst(varDef.name);
                if (sym != null) {
                    log.error(varDef.Pos, messages.duplicateMember, varDef.name, membersScope.owner.name);
                } else {
                    VarSymbol var = new VarSymbol(Kind.FIELD, varDef.name, (Type)scan(varDef.type, compoundScope),
                        membersScope.owner);
                    membersScope.enter(var);
                    varDef.symbol = var;
                }
                return null;
            }

            public override object visitFuncDef(FuncDef func, WritableScope enclScope)
            {
                FuncSymbol fsym = makeFuncSymbol(func, enclScope);

                if (enclScope == symtab.topLevelSymbol.topScope && func.name == "main") {
                    mainFuncFound = true;
                    check.checkMainFunction(func.Pos, fsym);
                }

                if (check.checkUnique(func.Pos, fsym, enclScope)) {
                    enclScope.enter(fsym);
                }
                return null;
            }

            private FuncSymbol makeFuncSymbol(FuncDef func, WritableScope enclScope)
            {
                FuncSymbol fsym = new FuncSymbol(func.name, enclScope.owner, null);
                func.symbol = fsym;

                fsym.owner = enclScope.owner;

                // create scope for func parameters and local variables
                WritableScope funcScope = enclScope.subScope(fsym);
                fsym.scope = funcScope;

                fsym.parameters = new List<VarSymbol>(func.parameters.Count);
                FuncType ftype = signature(func.returnType, func.parameters, funcScope);
                ftype.symbol = fsym;
                fsym.type = ftype;
                return fsym;
            }

            private FuncType signature(TypeTree retTypeTree, IList<VariableDeclaration> paramTrees,
                                       WritableScope scope)
            {
                // enter params into func scope
                foreach (VariableDeclaration paramTree in paramTrees) {
                    enterParameter(paramTree, scope);
                }

                // get return type
                Type retType = (Type)scan(retTypeTree, scope);

                Type[] paramTypes = new Type[paramTrees.Count];
                for (var i = 0; i < paramTrees.Count; i++) {
                    VariableDeclaration tree = paramTrees[i];
                    paramTypes[i] = (Type)scan(tree.type, scope);
                }

                return new FuncType(paramTypes, retType);
            }
            
            private void enterParameter(VariableDeclaration varDef, WritableScope scope)
            {
                Type       varType = (Type)scan(varDef.type, scope);
                FuncSymbol func    = (FuncSymbol)scope.owner;
                VarSymbol  varSym  = new VarSymbol(Kind.PARAM, varDef.name, varType, func);

                varDef.symbol = varSym;

                func.parameters.Add(varSym);

                if (check.checkUniqueParam(varDef.Pos, varSym, scope)) {
                    scope.enter(varSym);
                }
            }

            public override object visitPrimitiveType(PrimitiveTypeNode prim, WritableScope scope)
            {
                return symtab.typeForTag(prim.type);
            }

            public override object visitDeclaredType(DeclaredType declaredType, WritableScope scope)
            {
                Symbol sym = scope.findFirst(declaredType.name, s => (s.kind & Kind.STRUCT) != 0);
                if (sym != null) {
                    return sym.type;
                }
                log.error(declaredType.Pos, messages.undefinedType, declaredType.name);
                return symtab.errorType;
            }

            public override object visitArrayType(ArrayTypeTree arrayType, WritableScope scope)
            {
                return symtab.arrayTypeOf((Type)scan(arrayType.elemTypeTree, scope));
            }
        }
    }
}
