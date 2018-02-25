namespace Xifias.Tests

open Microsoft.FSharp.Reflection
open Microsoft.VisualStudio.TestTools.UnitTesting

module Helpers =
    let caseName (x : 'a) =
        match FSharpValue.GetUnionFields(x, typeof<'a>) with
            | case, _ -> case.Name

    let printFail (expected : 'a) (actual : 'a) =
        sprintf "\r\n\r\nEXPECTED\r\n%A\r\n\r\nACTUAL\r\n%A\r\n" expected actual


    let areEqual (expected: 'a) (actual: 'a) =
         Assert.IsTrue((expected = actual), printFail expected actual)
