using System.Security.Cryptography.X509Certificates;
using TerraNet.Relay;

var options = RelayOptions.Load(args.FirstOrDefault() ?? Environment.GetEnvironmentVariable("TERRANET_RELAY_CONFIG"));
var certificate = X509CertificateLoader.LoadPkcs12FromFile(options.CertificatePath, Environment.GetEnvironmentVariable("TERRANET_RELAY_CERT_PASSWORD"));

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    shutdown.Cancel();
};
AppDomain.CurrentDomain.ProcessExit += (_, _) => shutdown.Cancel();

await new RelayServer(options, certificate).RunAsync(shutdown.Token);
