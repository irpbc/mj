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
    /// Constructs symbols for declarations, lie methods and method parameters.
    /// Does not go into method bodies. That is done 
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
        private readonly Typings typings;
        private readonly Log log;

        public DeclarationAnalysis(Context ctx)
        {
            ctx.put(CONTEXT_KEY, this);

            symtab = Symtab.instance(ctx);
            check = Check.instance(ctx);
            typings = Typings.instance(ctx);
            log = Log.instance(ctx);
        }

        private bool mainMethodFound = false;

        public IList<CompilationUnit> main(IList<CompilationUnit> trees)
        {
            WriteableScope topScope = symtab.topLevelSymbol.topScope;

            foreach (CompilationUnit tree in trees) {
                SourceFile prevSource = log.useSource(tree.sourceFile);
                try {
                    analyze(tree, topScope);
                } finally {
                    log.useSource(prevSource);
                }
            }

            if (!mainMethodFound) {
                log.globalError(messages.mainMethodNotDefined);
            }
            
            return trees;
        }

        private void analyze(IEnumerable<Tree> trees, WriteableScope s)
        {
            foreach (Tree tree in trees) {
                analyze(tree, s);
            }
        }

        private void analyze(Tree tree, WriteableScope s) => tree.accept(this, s);

        public override object visitCompilationUnit(CompilationUnit compilationUnit, WriteableScope s)
        {
            // analyze methods -> go to visitMethodDef
            analyze(compilationUnit.declarations, s);
            return null;
        }

        public override object visitMethodDef(MethodDef method, WriteableScope enclScope)
        {
            Symbol owner = symtab.topLevelSymbol;

            MethodSymbol msym = makeMethodSymbol(method, enclScope, owner);

            if (method.name == "main") {
                mainMethodFound = true;
                check.checkMainMethod(method.Pos, msym);
            }
            
            if (check.checkUnique(method.Pos, msym, enclScope)) {
                enclScope.enter(msym);
            }
            return null;
        }

        private MethodSymbol makeMethodSymbol(MethodDef method, WriteableScope enclScope, Symbol owner)
        {
            MethodSymbol msym = new MethodSymbol(method.name, owner, null);
            method.symbol = msym;

            // create scope for method parameters and local variables
            WriteableScope methodScope = enclScope.subScope(msym);
            msym.scope = methodScope;

            msym.parameters = new List<VarSymbol>(method.parameters.Count);
            msym.type = signature(method.returnType, method.parameters, methodScope);
            msym.type.definer = msym;
            return msym;
        }

        public override object visitAspectDef(AspectDef aspect, WriteableScope enclScope)
        {
            Symbol owner = symtab.topLevelSymbol;
            
            AspectSymbol asym = new AspectSymbol(aspect.name, owner);

            if (check.checkUnique(aspect.Pos, asym, enclScope)) {
                enclScope.enter(asym);
                asym.afterMethod = makeMethodSymbol(aspect.after, enclScope.subScope(), asym);
            }
            
            return null;
        }

        private MethodType signature(TypeTree retTypeTree, IList<VariableDeclaration> paramTrees, WriteableScope scope)
        {
            // enter params into method scope
            analyze(paramTrees, scope);

            // get return type
            Type retType = typings.resolveType(retTypeTree);

            Type[] paramTypes = new Type[paramTrees.Count];
            for (var i = 0; i < paramTrees.Count; i++) {
                VariableDeclaration tree = paramTrees[i];
                paramTypes[i] = typings.resolveType(tree.type);
            }

            return new MethodType(paramTypes, retType);
        }

        public override object visitVarDef(VariableDeclaration varDef, WriteableScope scope)
        {
            Type varType = typings.resolveType(varDef.type);
            MethodSymbol met = (MethodSymbol)scope.owner;
            VarSymbol varSym = new VarSymbol(Kind.PARAM, varDef.name, varType, met);

            varDef.symbol = varSym;

            met.parameters.Add(varSym);

            if (check.checkUniqueParam(varDef.Pos, varSym, scope)) {
                scope.enter(varSym);
            }

            return null;
        }
    }
}
