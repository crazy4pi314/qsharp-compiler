namespace Microsoft.Quantum.Demo {
    
    /// # Summary
    /// Entry point for the demo. 
    ///
    /// # Example
    /// ```qsharp
    /// function TestIncrement1 () : Unit {
    ///     let output = Microsoft.Quantum.Demo.Increment(1);
    ///     EqualityFactI(2, output, "wrong return value");
    /// }
    /// ```
    /// ```qsharp
    /// function TestIncrement2 () : Unit {
    ///     let output = Microsoft.Quantum.Demo.Increment(2);
    ///     EqualityFactI(3, output, "wrong return value");
    /// }
    /// ```
    @EntryPoint()
    function Increment(arg : Int) : Int {
        return arg + 1;
    }
}
