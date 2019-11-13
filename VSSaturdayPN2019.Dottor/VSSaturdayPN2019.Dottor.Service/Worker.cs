using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VSSaturdayPN2019.Dottor.Web;

namespace VSSaturdayPN2019.Dottor.Service
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;
        private FileSystemWatcher _fileSystemWatcher;
        private readonly Sync.SyncClient _syncClient;
        private Grpc.Core.AsyncDuplexStreamingCall<NotifyChangeRequest, NotifyChangeReply> _notifyChangeCall;

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            var channel = GrpcChannel.ForAddress("https://localhost:50051");
            // create the client
            _syncClient = new Sync.SyncClient(channel);
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await _syncClient.NotifyStatusAsync(new NotifyStatusRequest()
            {
                AppName = "VSSaturdayPN2019.Dottor.Service",
                MonitorFolder = _configuration.GetValue<string>("FileSystemWatcher:Folder"),
                Status = "START"
            });

            await base.StartAsync(cancellationToken);
        }
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await _syncClient.NotifyStatusAsync(new NotifyStatusRequest()
            {
                AppName = "VSSaturdayPN2019.Dottor.Service",
                MonitorFolder = _configuration.GetValue<string>("FileSystemWatcher:Folder"),
                Status = "STOP"
            });

            await _notifyChangeCall.RequestStream.CompleteAsync();

            await base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _fileSystemWatcher = new FileSystemWatcher
            {
                Filter = "*.*",
                Path = _configuration.GetValue<string>("FileSystemWatcher:Folder"),
                IncludeSubdirectories = true,
            };
            _fileSystemWatcher.Created += MonikerChange;
            _fileSystemWatcher.Deleted += MonikerChange;
            _fileSystemWatcher.EnableRaisingEvents = true;

            _notifyChangeCall = _syncClient.NotifyChange();

            await Task.CompletedTask;
        }

        private void MonikerChange(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine($"{e.ChangeType} - {e.Name}");
            
            _notifyChangeCall.RequestStream.WriteAsync(new NotifyChangeRequest
            {
                MessageId = Guid.NewGuid().ToString(),
                ChangeType = e.ChangeType.ToString(),
                Name = e.Name
            }).GetAwaiter().GetResult();

        }

    }
}
