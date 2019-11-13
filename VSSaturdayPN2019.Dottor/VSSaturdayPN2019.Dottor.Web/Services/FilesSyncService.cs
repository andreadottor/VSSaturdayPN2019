using Grpc.Core;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSSaturdayPN2019.Dottor.Web.Hubs;

namespace VSSaturdayPN2019.Dottor.Web.Services
{
    public class FilesSyncService : Sync.SyncBase
    {
        private readonly ILogger<FilesSyncService> _logger;
        private readonly IHubContext<FilesSyncHub> _hubContext;
        private readonly BroacastEventService _broacastEventService;
        public FilesSyncService(ILogger<FilesSyncService> logger, IHubContext<FilesSyncHub> hubContext, BroacastEventService broacastEventService)
        {
            _logger = logger;
            _hubContext = hubContext;
            _broacastEventService = broacastEventService;
        }

        public override async Task NotifyChange(
                IAsyncStreamReader<NotifyChangeRequest> requestStream,
                IServerStreamWriter<NotifyChangeReply> responseStream,
                ServerCallContext context)
        {
            _logger.LogInformation("Start NotifyChange");
            while (await requestStream.MoveNext(CancellationToken.None))
            {
                var message = requestStream.Current;
                var response = new NotifyChangeReply()
                {
                    MessageId = message.MessageId
                };

                try
                {
                    _broacastEventService.NotifyChange((WatcherChangeTypes)Enum.Parse(typeof(WatcherChangeTypes), message.ChangeType), "", message.Name);
                    //var extension = Path.GetExtension(message.Name);
                    //await _hubContext.Clients.All.SendAsync("NotifyChange", message.ChangeType, message.Name, extension);
                    response.Status = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error on NotifyChange.", ex);
                    response.Status = false;
                }

                await responseStream.WriteAsync(response);
            }
        }

        public override async Task<NotifyStatusReply> NotifyStatus(NotifyStatusRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"NotifyStatus: {request.AppName} {request.AppName}");
            await _hubContext.Clients.All.SendAsync("ChangeStatus", request.Status, request.MonitorFolder);

            return new NotifyStatusReply
            {
                Status = true
            };
        }
    }
}
