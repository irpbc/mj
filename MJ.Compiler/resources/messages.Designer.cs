﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace mj.compiler.resources {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    public class messages {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal messages() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("mj.compiler.resources.messages", typeof(messages).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Array length must be of type integer or smaller.
        /// </summary>
        public static string arrayLengthType {
            get {
                return ResourceManager.GetString("arrayLengthType", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Assignment must be to a variable, field or array element.
        /// </summary>
        public static string assignmentLHS {
            get {
                return ResourceManager.GetString("assignmentLHS", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Uncompatible types in assignment.
        /// </summary>
        public static string assignmentUncompatible {
            get {
                return ResourceManager.GetString("assignmentUncompatible", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Break statement must be inside a loop or a switch.
        /// </summary>
        public static string breakNotInLoopSwitch {
            get {
                return ResourceManager.GetString("breakNotInLoopSwitch", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Case expression has to be a constant of same type as switch expression.
        /// </summary>
        public static string caseExpressionType {
            get {
                return ResourceManager.GetString("caseExpressionType", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Condition in a conditional expression must return boolean.
        /// </summary>
        public static string conditionalBoolean {
            get {
                return ResourceManager.GetString("conditionalBoolean", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Type mismatch of branches in a conditional expression.
        /// </summary>
        public static string conditionalMismatch {
            get {
                return ResourceManager.GetString("conditionalMismatch", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Continue statement must be inside a loop.
        /// </summary>
        public static string continueNotInLoop {
            get {
                return ResourceManager.GetString("continueNotInLoop", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Condition expression in a &quot;do while&quot; statement must return boolean.
        /// </summary>
        public static string doWhileConditonType {
            get {
                return ResourceManager.GetString("doWhileConditonType", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Duplicate case labels.
        /// </summary>
        public static string duplicateCaseLabels {
            get {
                return ResourceManager.GetString("duplicateCaseLabels", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Type {0} is already defined.
        /// </summary>
        public static string duplicateClassName {
            get {
                return ResourceManager.GetString("duplicateClassName", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Method {0} is already defiend.
        /// </summary>
        public static string duplicateMethodName {
            get {
                return ResourceManager.GetString("duplicateMethodName", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Parameter {0} is already defined in method {1}.
        /// </summary>
        public static string duplicateParamName {
            get {
                return ResourceManager.GetString("duplicateParamName", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Local variable {0} is already defined in this scope.
        /// </summary>
        public static string duplicateVar {
            get {
                return ResourceManager.GetString("duplicateVar", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Only incremet, decrement, assignment and invokation can be expression statements.
        /// </summary>
        public static string expressionStatement {
            get {
                return ResourceManager.GetString("expressionStatement", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Floating point literal {0} is too big.
        /// </summary>
        public static string floatLiteratTooBig {
            get {
                return ResourceManager.GetString("floatLiteratTooBig", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Condition expression in a &quot;for&quot; statement must return boolean.
        /// </summary>
        public static string forConditonType {
            get {
                return ResourceManager.GetString("forConditonType", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Condition expression in an &quot;if&quot; statement must return boolean.
        /// </summary>
        public static string ifConditonType {
            get {
                return ResourceManager.GetString("ifConditonType", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Incremet or decrement can only be on a variable.
        /// </summary>
        public static string incDecArgument {
            get {
                return ResourceManager.GetString("incDecArgument", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Base of index expression must be an array.
        /// </summary>
        public static string indexNotArray {
            get {
                return ResourceManager.GetString("indexNotArray", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Index of index expression must be of integer type.
        /// </summary>
        public static string indexNotInteger {
            get {
                return ResourceManager.GetString("indexNotInteger", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Integer literal {0} is too big.
        /// </summary>
        public static string intLiteratTooBig {
            get {
                return ResourceManager.GetString("intLiteratTooBig", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The main method is not defined.
        /// </summary>
        public static string mainMethodNotDefined {
            get {
                return ResourceManager.GetString("mainMethodNotDefined", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Main method must accept int and long and return int.
        /// </summary>
        public static string mainMethodSig {
            get {
                return ResourceManager.GetString("mainMethodSig", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Method &quot;{0}&quot; is not defined.
        /// </summary>
        public static string methodNotDefined {
            get {
                return ResourceManager.GetString("methodNotDefined", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Missing return statement.
        /// </summary>
        public static string missingReturnStatement {
            get {
                return ResourceManager.GetString("missingReturnStatement", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Multiple default cases in switch.
        /// </summary>
        public static string multipleDefaults {
            get {
                return ResourceManager.GetString("multipleDefaults", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Argument type mismatch in a call to method &quot;{0}&quot;.
        /// </summary>
        public static string paramTypeMismatch {
            get {
                return ResourceManager.GetString("paramTypeMismatch", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to You must return a value from a non-void method.
        /// </summary>
        public static string returnNonVoidMethod {
            get {
                return ResourceManager.GetString("returnNonVoidMethod", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Return type mismatch.
        /// </summary>
        public static string returnTypeMismatch {
            get {
                return ResourceManager.GetString("returnTypeMismatch", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to You can&apos;t return a value from a void method.
        /// </summary>
        public static string returnVoidMethod {
            get {
                return ResourceManager.GetString("returnVoidMethod", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Attempted field access on primitive type.
        /// </summary>
        public static string selectOnPrimitive {
            get {
                return ResourceManager.GetString("selectOnPrimitive", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Switch expression must be of integral type.
        /// </summary>
        public static string switchSelectorType {
            get {
                return ResourceManager.GetString("switchSelectorType", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Variable {0} might not have been assigned.
        /// </summary>
        public static string unassignedVariable {
            get {
                return ResourceManager.GetString("unassignedVariable", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Aspect &quot;{0}&quot; is undefined at this point.
        /// </summary>
        public static string undefinedAspect {
            get {
                return ResourceManager.GetString("undefinedAspect", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Type &quot;{0}&quot; is not defined.
        /// </summary>
        public static string undefinedClass {
            get {
                return ResourceManager.GetString("undefinedClass", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unknown type &quot;{0}&quot;.
        /// </summary>
        public static string undefinedType {
            get {
                return ResourceManager.GetString("undefinedType", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Variable &quot;{0}&quot; is undefined at this point.
        /// </summary>
        public static string undefinedVariable {
            get {
                return ResourceManager.GetString("undefinedVariable", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Field &quot;{0}&quot; is not defined in struct &quot;{1}&quot;.
        /// </summary>
        public static string unknownField {
            get {
                return ResourceManager.GetString("unknownField", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unreachable code detected.
        /// </summary>
        public static string unreachableCodeDetected {
            get {
                return ResourceManager.GetString("unreachableCodeDetected", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Operator &quot;{0}&quot; cannot be applied to types &quot;{1}&quot; and &quot;{2}&quot;.
        /// </summary>
        public static string unresolvedBinaryOperator {
            get {
                return ResourceManager.GetString("unresolvedBinaryOperator", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Operator &quot;{0}&quot; cannot be applied to type &quot;{1}&quot;.
        /// </summary>
        public static string unresolvedUnaryOperator {
            get {
                return ResourceManager.GetString("unresolvedUnaryOperator", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Initializer of variable &quot;{0}&quot; returns &quot;{1}&quot; while &quot;{2}&quot; is required.
        /// </summary>
        public static string varInitTypeMismatch {
            get {
                return ResourceManager.GetString("varInitTypeMismatch", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Local variable {0} has same name as a parameter.
        /// </summary>
        public static string varParamNameConflict {
            get {
                return ResourceManager.GetString("varParamNameConflict", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Condition expression in a &quot;while&quot; statement must return boolean.
        /// </summary>
        public static string whileConditonType {
            get {
                return ResourceManager.GetString("whileConditonType", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Method &quot;{0}&quot; takes {1} arguments, but {2} was given.
        /// </summary>
        public static string wrongNumberOfArgs {
            get {
                return ResourceManager.GetString("wrongNumberOfArgs", resourceCulture);
            }
        }
    }
}
