using System;
using System.Collections.Generic;
using System.Linq;

using mj.compiler.main;
using mj.compiler.resources;
using mj.compiler.tree;
using mj.compiler.utils;

using static mj.compiler.symbol.Scope;
using static mj.compiler.symbol.Symbol;

namespace mj.compiler.symbol
{
    /// <summary>
    /// Constructs symbols for declarations, lie funcs and func parameters.
    /// Does not go into func bodies. That is done 
    /// </summary>
    /// <para>
    /// 
    /// </para>
    public class DeclarationAnalysis : AstVisitor<Object, WriteableScope>
    {
        private static readonly Context.Key<DeclarationAnalysis> CONTEXT_KEY = new Context.Key<DeclarationAnalysis>();

        public static DeclarationAnalysis instance(Context ctx)
        {
            if (ctx.tryGet(CONTEXT_KEY, out var instance)) {
                return instance;
            }
            return new DeclarationAnalysis(ctx);
        }

        private readonly Symtab symtab;
        private readonly Check check;
        private readonly Log log;

        public DeclarationAnalysis(Context ctx)
        {
            ctx.put(CONTEXT_KEY, this);

            symtab = Symtab.instance(ctx);
            check = Check.instance(ctx);
            log = Log.instance(ctx);
        }

        private bool mainFuncFound = false;

        public IList<CompilationUnit> main(IList<CompilationUnit> trees)
        {
            if (trees.Count == 0) {
                return CollectionUtils.emptyList<CompilationUnit>();
            }
            WriteableScope topScope = symtab.topLevelSymbol.topScope;

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

            return trees;
        }

        public override object visitCompilationUnit(CompilationUnit compilationUnit, WriteableScope s)
        {
            // First enter structs because they define new types
            for (var i = 0; i < compilationUnit.declarations.Count; i++) {
                Tree decl = compilationUnit.declarations[i];
                if (decl.Tag == Tag.STRUCT_DEF) {
                    scan(decl, s);
                }
            }
            // Then enter other declarations
            for (var i = 0; i < compilationUnit.declarations.Count; i++) {
                Tree decl = compilationUnit.declarations[i];
                if (decl.Tag != Tag.STRUCT_DEF) {
                    scan(decl, s);
                }
            }
            return null;
        }

        public override object visitStructDef(StructDef structDef, WriteableScope enclScope)
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

        public override object visitFuncDef(FuncDef func, WriteableScope enclScope)
        {
            FuncSymbol fsym = makeFuncSymbol(func, enclScope);

            if (func.name == "main") {
                mainFuncFound = true;
                check.checkMainFunction(func.Pos, fsym);
            }

            if (check.checkUnique(func.Pos, fsym, enclScope)) {
                enclScope.enter(fsym);
            }
            return null;
        }

        private FuncSymbol makeFuncSymbol(FuncDef func, WriteableScope enclScope)
        {
            FuncSymbol fsym = new FuncSymbol(func.name, enclScope.owner, null);
            func.symbol = fsym;

            // create scope for func parameters and local variables
            WriteableScope funcScope = enclScope.subScope(fsym);
            fsym.scope = funcScope;

            fsym.parameters = new List<VarSymbol>(func.parameters.Count);
            fsym.type = signature(func.returnType, func.parameters, funcScope);
            return fsym;
        }

        private FuncType signature(TypeTree retTypeTree, IList<VariableDeclaration> paramTrees, WriteableScope scope)
        {
            // enter params into func scope
            scan(paramTrees, scope);

            // get return type
            Type retType = (Type)scan(retTypeTree, scope);

            Type[] paramTypes = new Type[paramTrees.Count];
            for (var i = 0; i < paramTrees.Count; i++) {
                VariableDeclaration tree = paramTrees[i];
                paramTypes[i] = (Type)scan(tree.type, scope);
            }

            return new FuncType(paramTypes, retType);
        }

        public override object visitVarDef(VariableDeclaration varDef, WriteableScope scope)
        {
            Type varType = (Type)scan(varDef.type, scope);
            FuncSymbol func = (FuncSymbol)scope.owner;
            VarSymbol varSym = new VarSymbol(Kind.PARAM, varDef.name, varType, func);

            varDef.symbol = varSym;

            func.parameters.Add(varSym);

            if (check.checkUniqueParam(varDef.Pos, varSym, scope)) {
                scope.enter(varSym);
            }

            return null;
        }

        public override object visitPrimitiveType(PrimitiveTypeNode prim, WriteableScope scope)
        {
            return symtab.typeForTag(prim.type);
        }

        public override object visitDeclaredType(DeclaredType declaredType, WriteableScope scope)
        {
            Symbol sym = scope.findFirst(declaredType.name, s => (s.kind & Kind.STRUCT) != 0);
            if (sym != null) {
                return sym.type;
            }
            log.error(declaredType.Pos, messages.undefinedType, declaredType.name);
            return symtab.errorType;
        }

        public override object visitArrayType(ArrayTypeTree arrayType, WriteableScope scope)
        {
            return symtab.arrayTypeForElemType((Type)scan(arrayType.elemTypeTree, scope));
        }
    }
}
