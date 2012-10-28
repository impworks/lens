module Lens.Parser.Node

open Lens.SyntaxTree.SyntaxTree
open Lens.SyntaxTree.SyntaxTree.ControlFlow
open Lens.SyntaxTree.SyntaxTree.Literals
open Lens.SyntaxTree.SyntaxTree.Operators
open Lens.SyntaxTree.Utils

// Special nodes
let using _ = failwith "Using node is currently not exist"

// Definitions
let recordEntry(entryName, typeName) =
    new RecordEntry(Name = entryName, Type = new TypeSignature(typeName))

let record(name, entries : RecordEntry list) =
    // TODO: Name a record.
    new RecordDefinitionNode(Fields = new ResizeArray<RecordEntry>(entries)) :> NodeBase

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
