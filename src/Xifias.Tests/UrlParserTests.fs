namespace Xifias.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open Xifias.UrlParser
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Internal
open Xifias.Tests.Helpers


type TestRecord = { Stringy : string; Inty : int }


[<TestClass>]
type UrlParsing() =

    let q : IQueryCollection = new QueryCollection(0) :> IQueryCollection



    [<TestMethod>]
    member __.``parse ignores leading and trailing slashes`` () =
        let actual = parse (s "test") q "test" |> Option.map (fun f -> f 1)
        areEqual (Some 1) actual
        let actual = parse (s "test") q "/test" |> Option.map (fun f -> f 1)
        areEqual (Some 1) actual
        let actual = parse (s "test") q "test/" |> Option.map (fun f -> f 1)
        areEqual (Some 1) actual
        let actual = parse (s "test") q "/test/" |> Option.map (fun f -> f 1)
        areEqual (Some 1) actual
        let actual = parse (s "test") q "/test/foo" |> Option.map (fun f -> f 1)
        areEqual None actual


    (*
        s parser
    *)

    [<TestMethod>]
    member __.``s parser returns Some when provided string matches`` () =
        let actual = parse (s "test") q "/test" |> Option.map (fun f -> f 1)
        areEqual (Some 1) actual

    [<TestMethod>]
    member __.``s parser returns None when provided string does not match`` () =
        let actual = parse (s "test") q "/grade" |> Option.map (fun f -> f 1)
        areEqual None actual


    (*
        string parser
    *)

    [<TestMethod>]
    member __.``string parser returns Some when provided string matches`` () =
        let actual = parse (string) q "/test"
        areEqual (Some "test") actual

    [<TestMethod>]
    member __.``string parser returns None when provided string part is empty`` () =
        let actual = parse (string) q "/"
        areEqual None actual


    (*
        int parser
    *)

    [<TestMethod>]
    member __.``int parser returns Some when provided string matches`` () =
        let actual = parse (int) q "/123"
        areEqual (Some 123) actual

    [<TestMethod>]
    member __.``int parser returns None when provided string does not have a number`` () =
        let actual = parse (int) q "/test"
        areEqual None actual


    (*
        guid parser
    *)

    [<TestMethod>]
    member __.``guid parser returns Some when provided string matches with no dashes`` () =
        let x = System.Guid.NewGuid()
        let actual = parse (guid) q ("/" + x.ToString("N"))
        areEqual (Some x) actual

    [<TestMethod>]
    member __.``guid parser returns Some when provided string matches with dashes`` () =
        let x = System.Guid.NewGuid()
        let actual = parse (guid) q ("/" + x.ToString())
        areEqual (Some x) actual

    [<TestMethod>]
    member __.``guid parser returns None when provided string does not have a guid`` () =
        let actual = parse (guid) q "/test"
        areEqual None actual


    (*
        custom parser
    *)

    [<TestMethod>]
    member __.``custom parser returns Some when provided string matches`` () =
        let float = custom (fun s -> match System.Double.TryParse s with | false, _ -> Error "Not a float" | true, v -> Ok v)
        let actual = parse (float) q "/123.1"
        areEqual (Some 123.1) actual

    [<TestMethod>]
    member __.``custom parser returns None when provided string does not have a number`` () =
        let float = custom (fun s -> match System.Double.TryParse s with | false, _ -> Error "Not a float" | true, v -> Ok v)
        let actual = parse (float) q "/test"
        areEqual None actual


    (*
        </> parser
    *)

    [<TestMethod>]
    member __.``</> parser return Some when provided string has 2 segments`` () =
        let actual = parse (s "test" </> string) q "/test/foo"
        areEqual (Some "foo") actual

    [<TestMethod>]
    member __.``</> parser returns None when provided string has 1 segment`` () =
        let actual = parse (s "test" </> string) q "/test"
        areEqual None actual


    (*
        map parser
    *)

    [<TestMethod>]
    member __.``map demo - turn parameters from parser into a record`` () =
        let createRecord a b = { Stringy = a; Inty = b }
        let urlParser = s "test" </> string </> s "id" </> int
        let recordParser = map createRecord urlParser
        let actual = parse recordParser q "/test/asdf/id/321"
        areEqual (Some (createRecord "asdf" 321)) actual



