module Lens.Parser.Node

open System
open System.Collections.Generic
open Lens.SyntaxTree.SyntaxTree
open Lens.SyntaxTree.SyntaxTree.ControlFlow
open Lens.SyntaxTree.SyntaxTree.Expressions
open Lens.SyntaxTree.SyntaxTree.Literals
open Lens.SyntaxTree.SyntaxTree.Operators
open Lens.SyntaxTree.Utils

type Accessor =
| Member of string
| Indexer of NodeBase

// Special nodes
let using nameSpace =
    UsingNode(Namespace = nameSpace) :> NodeBase

// Definitions
let typeTag nameSpace name additional =
    [nameSpace; Some name; additional]
    |> Seq.filter Option.isSome
    |> Seq.map Option.get
    |> String.concat(String.Empty)

let typeParams types =
    types
    |> String.concat ","
    |> sprintf "<%s>"

let arrayDefinition braces =
    braces
    |> Seq.map (fun _ -> "[]")
    |> String.concat String.Empty

let recordEntry(entryName, typeName) =
    RecordEntry(Name = entryName, Type = TypeSignature(typeName))

let record(name, entries) =
    let node = RecordDefinitionNode(Name = name)
    entries |> Seq.iter (fun e -> node.Entries.Add e)
    node :> NodeBase

let typeEntry(name, typeDefinition) =
    let signature =
        match typeDefinition with
        | Some s -> TypeSignature(s)
        | None   -> null
    TypeEntry(Name = name, TagType = signature)

let typeNode(name, entries) =
    let node = TypeDefinitionNode(Name = name)
    entries |> Seq.iter (fun e -> node.Entries.Add e)
    node :> NodeBase

let functionParameters parameters =
    let dictionary = Dictionary<_, _>()
    
    parameters
    |> Seq.map (fun((name, flag), typeTag) ->
                    let modifier =
                        match flag with
                        | Some "ref" -> ArgumentModifier.Ref
                        | Some "out" -> ArgumentModifier.Out
                        | _          -> ArgumentModifier.In
                    FunctionArgument(Name = name, Modifier = modifier, Type = typeTag))
    |> Seq.iter (fun fa -> dictionary.Add(fa.Name, fa))
    
    dictionary

let functionNode name parameters body =
    NamedFunctionNode(Name = name, Arguments = parameters, Body = body) :> NodeBase

// Code
let codeBlock (lines : NodeBase list) =
    CodeBlockNode(Statements = ResizeArray<_>(lines))

let variableDeclaration binding name value =
    let node : NameDeclarationBase =
        match binding with
        | "let" -> upcast LetNode()
        | "var" -> upcast VarNode()
        | _     -> failwith "Unknown value binding type"
    node.Name <- name
    node.Value <- value
    node :> NodeBase

let assignment typeName identifier accessorChain value =
    let setter : Accessor -> AccessorNodeBase = function
        | Member(name)        -> upcast SetMemberNode(MemberName = name, Value = value)
        | Indexer(expression) -> upcast SetIndexNode(Index = expression, Value = value)
    let getter : Accessor -> AccessorNodeBase = function
        | Member(name)        -> upcast GetMemberNode(MemberName = name)
        | Indexer(expression) -> upcast GetIndexNode(Index = expression)

    let accessors = List.rev accessorChain
    let node = setter <| List.head accessors
    let result = List.fold
                 <| (fun (n : AccessorNodeBase) a ->
                       let newNode = getter a
                       n.Expression <- newNode
                       newNode)
                 <| node
                 <| List.tail accessors
                 :?> GetMemberNode

    if Option.isSome identifier then
        let memberName = Option.get identifier
        let topNode = GetMemberNode(MemberName = memberName, StaticType = TypeSignature(typeName))
        result.Expression <- topNode
    else
        result.StaticType <- TypeSignature(typeName)

    result :> NodeBase

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

// Literals
let int (value : string) = IntNode(Value = int value)

// Operators
let operatorNode symbol =
    match symbol with
    | "+" -> AddOperatorNode()
    | _   -> failwithf "Unknown operator %s" symbol

let private binaryOperator symbol left right =
    let node = operatorNode symbol
    node.LeftOperand <- left
    node.RightOperand <- right
    node :> NodeBase

let rec operatorChain (node, operations) =
    match operations with
    | [] -> node
    | (op, node2) :: other ->
        let newNode = binaryOperator op node node2
        operatorChain(newNode, other)
