module Lens.Parser.Node

open System
open Lens.SyntaxTree.SyntaxTree
open Lens.SyntaxTree.SyntaxTree.ControlFlow
open Lens.SyntaxTree.SyntaxTree.Literals
open Lens.SyntaxTree.SyntaxTree.Operators
open Lens.SyntaxTree.Utils

// Special nodes
let using nameSpace =
    new UsingNode(Namespace = nameSpace) :> NodeBase

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
    new RecordEntry(Name = entryName, Type = new TypeSignature(typeName))

let record(name, entries) =
    let node = new RecordDefinitionNode(Name = name)
    entries |> Seq.iter (fun e -> node.Entries.Add e)
    node :> NodeBase

let typeEntry(name, typeDefinition) =
    let signature =
        match typeDefinition with
        | Some s -> new TypeSignature(s)
        | None   -> null
    new TypeEntry(Name = name, TagType = signature)

let typeNode(name, entries) =
    let node = new TypeDefinitionNode(Name = name)
    entries |> Seq.iter (fun e -> node.Entries.Add e)
    node :> NodeBase

// Literals
let int (value : string) = new IntNode(Value = int value)

// Operators
let operatorNode symbol =
    match symbol with
    | "+" -> new AddOperatorNode()
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
