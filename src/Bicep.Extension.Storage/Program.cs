using System.Reflection;
using Bicep.Extension.Storage.Handlers;
using Bicep.Extension.Storage.Models;
using Bicep.Local.Extension.Host.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var assembly = typeof(Program).Assembly;
var assemblyName = assembly.GetName().Name ?? "bicep-ext-storage";
var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? assembly.GetName().Version?.ToString()
    ?? "0.0.0";

var builder = WebApplication.CreateBuilder();

builder.AddBicepExtensionHost(args);
builder.Services
    .AddBicepExtension()
    .WithDefaults(
        name: assemblyName.Split('-')[^1],
        version: informationalVersion.Split('+')[0],
        isSingleton: true)
    .WithTypeAssembly(typeof(Program).Assembly)
    .WithConfigurationType(typeof(Configuration))
    .WithResourceHandler<BlobContainerHandler>()
    .WithResourceHandler<BlobHandler>()
    .WithResourceHandler<DataLakeDirectoryHandler>()
    .WithResourceHandler<DataLakeFileSystemHandler>()
    .WithResourceHandler<FileShareHandler>()
    .WithResourceHandler<FileShareDirectoryHandler>()
    .WithResourceHandler<FileShareFileHandler>()
    .WithResourceHandler<QueueHandler>()
    .WithResourceHandler<TableHandler>()
    .WithResourceHandler<TableEntityHandler>();

var app = builder.Build();
app.MapBicepExtension();

await app.RunAsync();
