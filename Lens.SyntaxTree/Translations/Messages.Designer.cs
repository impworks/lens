﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.18034
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Lens.SyntaxTree.Translations {
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
    public class Messages {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Messages() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Lens.SyntaxTree.Translations.Messages", typeof(Messages).Assembly);
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
        ///   Looks up a localized string similar to Type &apos;{0}&apos; cannot be used in catch clause because it does not derive from System.Exception!.
        /// </summary>
        public static string CatchTypeNotException {
            get {
                return ResourceManager.GetString("CatchTypeNotException", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Cannot closure an implicit variable!.
        /// </summary>
        public static string ClosureImplicit {
            get {
                return ResourceManager.GetString("ClosureImplicit", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Cannot closure a ref argument!.
        /// </summary>
        public static string ClosureRef {
            get {
                return ResourceManager.GetString("ClosureRef", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Code block contains no statements!.
        /// </summary>
        public static string CodeBlockEmpty {
            get {
                return ResourceManager.GetString("CodeBlockEmpty", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to A declaration of a variable or a constant cannot be the last statement in a code block!.
        /// </summary>
        public static string CodeBlockLastVar {
            get {
                return ResourceManager.GetString("CodeBlockLastVar", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to No best common type found for return types of condition branches: &apos;{0}&apos; and &apos;{1}&apos; respectively..
        /// </summary>
        public static string ConditionInconsistentTyping {
            get {
                return ResourceManager.GetString("ConditionInconsistentTyping", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to A constructor must not be marked as static!.
        /// </summary>
        public static string ConstructorStatic {
            get {
                return ResourceManager.GetString("ConstructorStatic", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Context #{0} does not exist!.
        /// </summary>
        public static string ContextNotFound {
            get {
                return ResourceManager.GetString("ContextNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Context #{0} has been unregistered!.
        /// </summary>
        public static string ContextUnregistered {
            get {
                return ResourceManager.GetString("ContextUnregistered", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Expression type cannot be inferred! Please use type casting to specify actual type..
        /// </summary>
        public static string ExpressionNull {
            get {
                return ResourceManager.GetString("ExpressionNull", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Expression that returns a value is expected!.
        /// </summary>
        public static string ExpressionVoid {
            get {
                return ResourceManager.GetString("ExpressionVoid", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Number of generic arguments does not match!.
        /// </summary>
        public static string GenericArgCountMismatch {
            get {
                return ResourceManager.GetString("GenericArgCountMismatch", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Generic argument &apos;{0}&apos; has mismatched values: &apos;{1}&apos; and &apos;{2}&apos;!.
        /// </summary>
        public static string GenericArgMismatch {
            get {
                return ResourceManager.GetString("GenericArgMismatch", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Cannot apply generic arguments to non-generic method &apos;{0}&apos;!.
        /// </summary>
        public static string GenericArgsToNonGenericMethod {
            get {
                return ResourceManager.GetString("GenericArgsToNonGenericMethod", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Generic argument &apos;{0}&apos; could not be resolved!.
        /// </summary>
        public static string GenericArgumentNotResolved {
            get {
                return ResourceManager.GetString("GenericArgumentNotResolved", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Generic argument &apos;{0}&apos; was has hint type &apos;{1}&apos;, but inferred type was &apos;{2}&apos;!.
        /// </summary>
        public static string GenericHintMismatch {
            get {
                return ResourceManager.GetString("GenericHintMismatch", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Cannot resolve arguments of &apos;{0}&apos; using type &apos;{1}&apos;!.
        /// </summary>
        public static string GenericImplementationWrongType {
            get {
                return ResourceManager.GetString("GenericImplementationWrongType", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Cannot infer argument types of &apos;{0}&apos;: type &apos;{1}&apos; implements multiple overrides!.
        /// </summary>
        public static string GenericInterfaceMultipleImplementations {
            get {
                return ResourceManager.GetString("GenericInterfaceMultipleImplementations", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Type &apos;{0}&apos; does not implement any kind of interface &apos;{1}&apos;!.
        /// </summary>
        public static string GenericInterfaceNotImplemented {
            get {
                return ResourceManager.GetString("GenericInterfaceNotImplemented", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Generic parameter &apos;{0}&apos; was not found!.
        /// </summary>
        public static string GenericParameterNotFound {
            get {
                return ResourceManager.GetString("GenericParameterNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Entities cannot be imported into a saveable assembly!.
        /// </summary>
        public static string ImportIntoSaveableAssembly {
            get {
                return ResourceManager.GetString("ImportIntoSaveableAssembly", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Only public, static, non-generic methods can be imported!.
        /// </summary>
        public static string ImportUnsupportedMethod {
            get {
                return ResourceManager.GetString("ImportUnsupportedMethod", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Indexer is ambigious! At least two cases apply:{3}{0}[{1}]{3}{0}[{2}].
        /// </summary>
        public static string IndexAmbigious {
            get {
                return ResourceManager.GetString("IndexAmbigious", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Type &apos;{0}&apos; has no index getter that accepts an index of type &apos;{1}&apos;!.
        /// </summary>
        public static string IndexGetterNotFound {
            get {
                return ResourceManager.GetString("IndexGetterNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Type &apos;{0}&apos; has no index setter that accepts an index of type &apos;{1}&apos;!.
        /// </summary>
        public static string IndexSetterNotFound {
            get {
                return ResourceManager.GetString("IndexSetterNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Lambda return type cannot be inferred! Please use type casting to specify type..
        /// </summary>
        public static string LambdaReturnTypeUnknown {
            get {
                return ResourceManager.GetString("LambdaReturnTypeUnknown", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Property #{0} has no getter!.
        /// </summary>
        public static string PropertyIdNoGetter {
            get {
                return ResourceManager.GetString("PropertyIdNoGetter", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Property #{0} has no setter!.
        /// </summary>
        public static string PropertyIdNoSetter {
            get {
                return ResourceManager.GetString("PropertyIdNoSetter", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Property #{0} does not exist!.
        /// </summary>
        public static string PropertyIdNotFound {
            get {
                return ResourceManager.GetString("PropertyIdNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Property &apos;{0}&apos; has already been imported!.
        /// </summary>
        public static string PropertyImported {
            get {
                return ResourceManager.GetString("PropertyImported", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Function of type &apos;{0}&apos; cannot return an expression of type &apos;{1}&apos;!.
        /// </summary>
        public static string ReturnTypeMismatch {
            get {
                return ResourceManager.GetString("ReturnTypeMismatch", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Expression is expected! An exception can only be rethrown from a catch clause..
        /// </summary>
        public static string ThrowArgumentExpected {
            get {
                return ResourceManager.GetString("ThrowArgumentExpected", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Type &apos;{0}&apos; cannot be used in throw statement because it does not derive from System.Exception!.
        /// </summary>
        public static string ThrowTypeNotException {
            get {
                return ResourceManager.GetString("ThrowTypeNotException", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Type &apos;{0}&apos; has already been defined!.
        /// </summary>
        public static string TypeDefined {
            get {
                return ResourceManager.GetString("TypeDefined", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Ambigious type reference: type &apos;{0}&apos; is found in the following namespaces:{5}{1} in assembly {2}{5}{3} in assembly {4}.
        /// </summary>
        public static string TypeIsAmbiguous {
            get {
                return ResourceManager.GetString("TypeIsAmbiguous", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Type &apos;{0}&apos; could not be resolved!.
        /// </summary>
        public static string TypeNotFound {
            get {
                return ResourceManager.GetString("TypeNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to A variable named &apos;{0}&apos; is already defined!.
        /// </summary>
        public static string VariableDefined {
            get {
                return ResourceManager.GetString("VariableDefined", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to A variable named &apos;{0}&apos; does not exist in the scope!.
        /// </summary>
        public static string VariableNotFound {
            get {
                return ResourceManager.GetString("VariableNotFound", resourceCulture);
            }
        }
    }
}
