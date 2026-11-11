namespace by.pieckenst.debugtool.testapiconnectionstool
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
           
return await DiagnosticCli.RunAsync(args);

        }
    }
}
