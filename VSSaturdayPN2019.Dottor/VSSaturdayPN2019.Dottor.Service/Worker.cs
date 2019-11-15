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
        private readonly string _appName = "VSSaturdayPN2019.Dottor.Service";
        private readonly string _folder;

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _folder = _configuration.GetValue<string>("FileSystemWatcher:Folder");

            var channel = GrpcChannel.ForAddress("https://localhost:50051");
            _syncClient = new Sync.SyncClient(channel);
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await _syncClient.NotifyStatusAsync(new NotifyStatusRequest()
            {
                AppName = this._appName,
                MonitorFolder = this._folder,
                Status = "START"
            });

            await base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await _syncClient.NotifyStatusAsync(new NotifyStatusRequest()
            {
                AppName = this._appName,
                MonitorFolder = this._folder,
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
                Path = this._folder,
                IncludeSubdirectories = true,
            };
            _fileSystemWatcher.Created += ManageChange;
            _fileSystemWatcher.Deleted += ManageChange;
            _fileSystemWatcher.Renamed += ManageRename;
            _fileSystemWatcher.EnableRaisingEvents = true;

            _notifyChangeCall = _syncClient.NotifyChange();

            await Task.CompletedTask;
        }

        private void ManageChange(object sender, FileSystemEventArgs e)
        {
            _logger.LogInformation($"{e.ChangeType} - {e.Name}");
            
            _notifyChangeCall.RequestStream.WriteAsync(new NotifyChangeRequest
            {
                MessageId = Guid.NewGuid().ToString(),
                ChangeType = e.ChangeType.ToString(),
                Name = e.Name
            }).GetAwaiter().GetResult();
        }

        private void ManageRename(object sender, RenamedEventArgs e)
        {
            _logger.LogInformation($"Renamed - {e.OldName} in {e.Name}");

            // delete old
            //
            _notifyChangeCall.RequestStream.WriteAsync(new NotifyChangeRequest
            {
                MessageId = Guid.NewGuid().ToString(),
                ChangeType = WatcherChangeTypes.Deleted.ToString(),
                Name = e.OldName
            }).GetAwaiter().GetResult();

            // create new
            //
            _notifyChangeCall.RequestStream.WriteAsync(new NotifyChangeRequest
            {
                MessageId = Guid.NewGuid().ToString(),
                ChangeType = WatcherChangeTypes.Created.ToString(),
                Name = e.Name
            }).GetAwaiter().GetResult();
        }

    }
}
