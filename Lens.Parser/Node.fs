module Lens.Parser.Node

open System
open System.Collections.Generic
open Lens.Parser.Accessor
open Lens.Parser.Symbol
open Lens.SyntaxTree.Compiler
open Lens.SyntaxTree.SyntaxTree
open Lens.SyntaxTree.SyntaxTree.ControlFlow
open Lens.SyntaxTree.SyntaxTree.Expressions
open Lens.SyntaxTree.SyntaxTree.Literals
open Lens.SyntaxTree.SyntaxTree.Operators
open Lens.SyntaxTree.Translations
open Lens.Utils

// Special nodes
let using nameSpace =
    UsingNode(Namespace = nameSpace) :> NodeBase

// Definitions
let typeTag (fullName : string, additional : string option) : string =
    match additional with
    | Some s -> fullName + s
    | None   -> fullName

let typeSignature (fullName : string, additional : string option) : TypeSignature =
    TypeSignature(typeTag(fullName, additional))

let typeParams types =
    types
    |> String.concat ","
    |> sprintf "<%s>"

let arrayDefinition braces =
    braces
    |> Seq.map (fun _ -> "[]")
    |> String.concat String.Empty

let recordEntry(entryName, typeSig) =
    RecordField(Name = entryName, Type = typeSig)

let record name entries =
    let node = RecordDefinitionNode(Name = name)
    entries |> Seq.iter (fun e -> node.Entries.Add e)
    node :> NodeBase

let typeEntry name typeSig =
    let signature =
        match typeSig with
        | Some t -> t
        | None   -> null
    TypeLabel(Name = name, TagType = signature)

let typeNode name entries =
    let node = TypeDefinitionNode(Name = name)
    entries |> Seq.iter (fun e -> node.Entries.Add e)
    node :> NodeBase

let functionParameters parameters =
    let list = List<_>()
    
    parameters
    |> Seq.map (fun((name, flag), typeSig) ->
        let isRef = flag = Some "ref"
        FunctionArgument(Name = name, IsRefArgument = isRef, TypeSignature = typeSig))
    |> Seq.iter (fun fa -> list.Add(fa))
    
    list

let functionNode name typeSig parameters body =
    FunctionNode(
        Name = name,
        ReturnTypeSignature = (match typeSig with
                               | Some t -> t
                               | None   -> null),
        Arguments = parameters, Body = body) :> NodeBase

// Code
let codeBlock (lines : NodeBase list) =
    CodeBlockNode(Statements = ResizeArray<_>(lines))

let throw maybeExpression : NodeBase =
    upcast (match maybeExpression with
            | Some(expression) -> ThrowNode(Expression = expression)
            | None             -> ThrowNode())

let variableDeclaration binding name value =
    let node : NameDeclarationNodeBase =
        match binding with
        | "let" -> upcast LetNode()
        | "var" -> upcast VarNode()
        | other -> failwith <| String.Format(ParserMessages.UnknownValueBindingType, other)
    node.Name <- name
    node.Value <- value
    node :> NodeBase

let indexNode expression index : NodeBase =
    match index with
    | Some i -> upcast GetIndexNode(Expression = expression, Index = i)
    | None   -> expression

/// Generates the getter chain and connects it to the node. accessors must be reversed.
let getterChain node (accessors : Accessor list) =
    List.fold
    <| (fun (n : AccessorNodeBase) a ->
        let newNode = accessorGetter a
        n.Expression <- newNode
        newNode)
    <| node
    <| accessors

let staticSymbol(typeSig, symbolName) =
    Static(typeSig, symbolName)

let localSymbol name =
    Local name

let expressionSymbol(expression, accessor) =
    Expression(expression, accessor)

let assignment (symbol : Symbol, accessorChain) value : NodeBase =
    match accessorChain with
    | [] -> symbolSetter symbol value
    | _  -> let accessors = List.rev accessorChain
            let root = accessorSetter <| List.head accessors <| value
            let last = getterChain root <| List.tail accessors
            let top = symbolGetter symbol
            last.Expression <- top
            upcast root

let getterNode (symbol, accessorChain) =
    match accessorChain with
    | [] -> symbolGetter symbol
    | _  -> let accessors = List.rev accessorChain
            let root = accessorGetter <| List.head accessors
            let last = getterChain root <| List.tail accessors
            let top = symbolGetter symbol
            last.Expression <- top
            upcast root

let genericGetterNode (symbol, chain) (typeArguments : TypeSignature list option) : NodeBase =
    match typeArguments with
    | Some(arguments) ->
        let node : GetMemberNode = downcast getterNode (symbol, chain)
        node.TypeHints <- ResizeArray<_> arguments
        upcast node
    | None           ->
        getterNode (symbol, chain)

let lambda parameters code : NodeBase =
    let node = LambdaNode(Body = code)
    Option.iter
    <| fun p -> node.Arguments <- p
    <| parameters
    upcast node

let byRefArg (symbol, accessorChain) =
    let node = getterNode (symbol, accessorChain)
    (box node :?> IPointerProvider).PointerRequired <- true
    node

let invocation expression (parameters : NodeBase list) : NodeBase =
    upcast InvocationNode(Expression = expression, Arguments = ResizeArray<_> parameters)

let fluentCall (argument : NodeBase) (functions : (string * (NodeBase list)) list option) : NodeBase =
    match functions with
    | None            -> argument
    | Some(someFunctions) ->
        let rec loop (arg : NodeBase) (calls : (string * (NodeBase list)) list) =
            match calls with
            | []                           -> arg
            | (name, parameters) :: others ->
                let getNode = GetMemberNode(Expression = loop arg others, MemberName = name)
                upcast InvocationNode(Expression = getNode, Arguments = ResizeArray<_> parameters)
        loop argument (List.rev someFunctions)

// Branch constructions
let ifNode condition thenBlock elseBlock =
    let falseAction =
        match elseBlock with
        | Some a -> a
        | None   -> null
    ConditionNode(Condition = condition, TrueAction = thenBlock, FalseAction = falseAction) :> NodeBase

let whileNode condition block =
    LoopNode(Condition = condition, Body = block) :> NodeBase

let tryCatchNode expression catchClauses =
    let node = TryNode(Code = expression)
    node.CatchClauses.AddRange(catchClauses)
    node :> NodeBase

let catchNode variableDefinition code =
    let node =
        match variableDefinition with
        | Some (typeSig, variableName) -> CatchNode(
                                               ExceptionType = typeSig,
                                               ExceptionVariable = variableName)
        | None                          -> CatchNode()
    node.Code <- code
    node

// Literals
let unit _ : NodeBase =
    upcast UnitNode()

let nullNode _ : NodeBase =
    upcast NullNode()

let boolean value =
    let v = 
        match value with
        | "true"  -> true
        | "false" -> false
        | other   -> failwith <| String.Format(ParserMessages.UnknownBooleanValue, other)
    BooleanNode(Value = v) :> NodeBase

let int (value : string) =
    IntNode(Value = int value) :> NodeBase

let double (value : string) =
    DoubleNode(Value = double value) :> NodeBase

let string value =
    StringNode(Value = value) :> NodeBase

// Operators
let castNode expression castOption : NodeBase =
    match castOption with
    | None                -> expression
    | Some("is", typeSig) -> upcast IsOperatorNode(Expression = expression, TypeSignature = typeSig)
    | Some("as", typeSig) -> upcast CastOperatorNode(Expression = expression, TypeSignature = typeSig)
    | Some(other, name)   -> failwith <| String.Format(ParserMessages.UnknownCastOperator, other)

let binaryOperatorNode symbol : BinaryOperatorNodeBase =
    let booleanKind = function
    | "&&"  -> BooleanOperatorKind.And
    | "||"  -> BooleanOperatorKind.Or
    | "^^"  -> BooleanOperatorKind.Xor
    | other -> failwith <| String.Format(ParserMessages.UnknownLogicalOperator, other)

    let comparisonKind = function
    | "=="  -> ComparisonOperatorKind.Equals
    | "<>"  -> ComparisonOperatorKind.NotEquals
    | "<"   -> ComparisonOperatorKind.Less
    | ">"   -> ComparisonOperatorKind.Greater
    | "<="  -> ComparisonOperatorKind.LessEquals
    | ">="  -> ComparisonOperatorKind.GreaterEquals
    | other -> failwith <| String.Format(ParserMessages.UnknownComparisonOperator, other)

    match symbol with
    | "&&"
    | "||"
    | "^^"  -> upcast BooleanOperatorNode(Kind = booleanKind symbol)
    | "=="
    | "<>"
    | "<"
    | ">"
    | "<="
    | ">="  -> upcast ComparisonOperatorNode(Kind = comparisonKind symbol)
    | "**"  -> upcast PowOperatorNode()
    | "*"   -> upcast MultiplyOperatorNode()
    | "/"   -> upcast DivideOperatorNode()
    | "%"   -> upcast RemainderOperatorNode()
    | "+"   -> upcast AddOperatorNode()
    | "-"   -> upcast SubtractOperatorNode()
    | other -> failwith <| String.Format(ParserMessages.UnknownBinaryOperator, other)

let unaryOperator symbol operand : NodeBase =
    match symbol with
    | Some "not" -> upcast InversionOperatorNode(Operand = operand)
    | Some "-"   -> upcast NegationOperatorNode(Operand = operand)
    | Some other -> failwith <| String.Format(ParserMessages.UnknownUnaryOperator, other)
    | None       -> operand

let private binaryOperator symbol left right =
    let node = binaryOperatorNode symbol
    node.LeftOperand <- left
    node.RightOperand <- right
    node :> NodeBase

let rec operatorChain node operations =
    match operations with
    | [] -> node
    | (op, node2) :: other ->
        let newNode = binaryOperator op node node2
        operatorChain newNode other

let typeOperator symbol typeSig =
    let node : TypeOperatorNodeBase =
        match symbol with
        | "typeof"  -> upcast TypeofOperatorNode()
        | "default" -> upcast DefaultOperatorNode()
        | other     -> failwith <| String.Format(ParserMessages.UnknownTypeOperator, other)
    node.TypeSignature <- typeSig
    node :> NodeBase

// New objects
let dictEntry key value =
    KeyValuePair(key, value)

let objectNode typeSig (parameters : NodeBase list option) =
    let arguments =
        match parameters with
        | Some args -> ResizeArray<_> args
        | None      -> ResizeArray<_>()
    NewObjectNode(Type = typeSig, Arguments = arguments) :> NodeBase

let tupleNode (elements : NodeBase list) =
    NewTupleNode(Expressions = ResizeArray<_> elements) :> NodeBase

let listNode (elements: NodeBase list) : NodeBase =
    upcast NewListNode(Expressions = ResizeArray<_> elements)

let dictNode (elements : KeyValuePair<NodeBase, NodeBase> list) : NodeBase =
    upcast NewDictionaryNode(Expressions = ResizeArray<_> elements)

let arrayNode (elements : NodeBase list) =
    NewArrayNode(Expressions = ResizeArray<_> elements) :> NodeBase
