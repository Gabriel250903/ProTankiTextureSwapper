using System;
using System.IO;
using System.Security.Cryptography;

string privateKeyFile = "publisher_private_key.txt";
string installerPath = @"Output\TextureSwapper_Setup.exe";
string signaturePath = @"Output\TextureSwapper_Setup.exe.sig";

if (!File.Exists(privateKeyFile))
{
    Console.Error.WriteLine($"Error: Private key file ({privateKeyFile}) not found");
    Environment.Exit(1);
}

if (!File.Exists(installerPath))
{
    Console.Error.WriteLine($"Error: Compiled installer ({installerPath}) not found");
    Environment.Exit(1);
}

try
{
    Console.WriteLine("Reading private key");
    string privateKeyBase64 = File.ReadAllText(privateKeyFile).Trim();

    Console.WriteLine("Reading installer bytes");
    byte[] installerBytes = File.ReadAllBytes(installerPath);

    Console.WriteLine("Signing the installer");
    using var ecdsa = ECDsa.Create();
    ecdsa.ImportECPrivateKey(Convert.FromBase64String(privateKeyBase64), out _);
    byte[] signature = ecdsa.SignData(installerBytes, HashAlgorithmName.SHA256);

    Console.WriteLine($"Saving signature file to {signaturePath}");
    File.WriteAllBytes(signaturePath, signature);

    Console.WriteLine("\nInstaller signed successfully");
    Console.WriteLine($"1. {installerPath}");
    Console.WriteLine($"2. {signaturePath}");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error occurred: {ex.Message}");
    Environment.Exit(1);
}
