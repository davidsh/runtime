// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;

namespace System.Net
{
    [EventSource(Name = "Microsoft-System-Net-Http", LocalizationResources = "FxResources.System.Net.Http.SR")]
    internal sealed partial class NetEventSource : EventSource
    {
        private const int UriBaseAddressId = NextAvailableEventId;
        private const int ContentNullId = UriBaseAddressId + 1;
        private const int ClientSendCompletedId = ContentNullId + 1;
        private const int HeadersInvalidValueId = ClientSendCompletedId + 1;
        private const int HandlerMessageId = HeadersInvalidValueId + 1;
        private const int AuthenticationInfoId = HandlerMessageId + 1;
        private const int AuthenticationErrorId = AuthenticationInfoId + 1;
        private const int HandlerErrorId = AuthenticationErrorId + 1;
        private const int RequestStartId = HandlerErrorId + 1;
        private const int RequestStopId = RequestStartId + 1;

        private IncrementingPollingCounter? _requestsPerSecondCounter;
        private PollingCounter? _totalRequestsCounter;
        private PollingCounter? _failedRequestsCounter;
        private PollingCounter? _currentRequestsCounter;
        private EventCounter? _queueDuration;

        private long _totalRequests;
        private long _currentRequests;
        private long _failedRequests;

        [NonEvent]
        public static void UriBaseAddress(object obj, Uri? baseAddress)
        {
            Debug.Assert(IsEnabled);
            Log.UriBaseAddress(baseAddress?.ToString(), IdOf(obj), GetHashCode(obj));
        }

        [Event(UriBaseAddressId, Keywords = Keywords.Debug, Level = EventLevel.Informational)]
        private unsafe void UriBaseAddress(string? uriBaseAddress, string objName, int objHash) =>
            WriteEvent(UriBaseAddressId, uriBaseAddress, objName, objHash);

        [NonEvent]
        public static void ContentNull(object obj)
        {
            Debug.Assert(IsEnabled);
            Log.ContentNull(IdOf(obj), GetHashCode(obj));
        }

        [Event(ContentNullId, Keywords = Keywords.Debug, Level = EventLevel.Informational)]
        private void ContentNull(string objName, int objHash) =>
            WriteEvent(ContentNullId, objName, objHash);

        [NonEvent]
        public static void ClientSendCompleted(HttpClient httpClient, HttpResponseMessage response, HttpRequestMessage request)
        {
            Debug.Assert(IsEnabled);
            Log.ClientSendCompleted(response?.ToString(), GetHashCode(request), GetHashCode(response), GetHashCode(httpClient));
        }

        [Event(ClientSendCompletedId, Keywords = Keywords.Debug, Level = EventLevel.Verbose)]
        private void ClientSendCompleted(string? responseString, int httpRequestMessageHash, int httpResponseMessageHash, int httpClientHash) =>
            WriteEvent(ClientSendCompletedId, responseString, httpRequestMessageHash, httpResponseMessageHash, httpClientHash);

        [Event(HeadersInvalidValueId, Keywords = Keywords.Debug, Level = EventLevel.Error)]
        public void HeadersInvalidValue(string name, string rawValue) =>
            WriteEvent(HeadersInvalidValueId, name, rawValue);

        [Event(HandlerMessageId, Keywords = Keywords.Debug, Level = EventLevel.Verbose)]
        public void HandlerMessage(int poolId, int workerId, int requestId, string? memberName, string? message) =>
            WriteEvent(HandlerMessageId, poolId, workerId, requestId, memberName, message);

        [Event(HandlerErrorId, Keywords = Keywords.Debug, Level = EventLevel.Error)]
        public void HandlerMessageError(int poolId, int workerId, int requestId, string? memberName, string message) =>
            WriteEvent(HandlerErrorId, poolId, workerId, requestId, memberName, message);

        [NonEvent]
        public static void AuthenticationInfo(Uri uri, string message)
        {
            Debug.Assert(IsEnabled);
            Log.AuthenticationInfo(uri?.ToString(), message);
        }

        [Event(AuthenticationInfoId, Keywords = Keywords.Debug, Level = EventLevel.Verbose)]
        public void AuthenticationInfo(string? uri, string message) =>
            WriteEvent(AuthenticationInfoId, uri, message);

        [NonEvent]
        public static void AuthenticationError(Uri? uri, string message)
        {
            Debug.Assert(IsEnabled);
            Log.AuthenticationError(uri?.ToString(), message);
        }

        [Event(AuthenticationErrorId, Keywords = Keywords.Debug, Level = EventLevel.Error)]
        public void AuthenticationError(string? uri, string message) =>
            WriteEvent(AuthenticationErrorId, uri, message);

        // NOTE
        // - The 'Start' and 'Stop' suffixes on the following event names have special meaning in EventSource. They
        //   enable creating 'activities'.
        //   For more information, take a look at the following blog post:
        //   https://blogs.msdn.microsoft.com/vancem/2015/09/14/exploring-eventsource-activity-correlation-and-causation-features/
        // - A stop event's event id must be next one after its start event.
        [Event(RequestStartId, Level = EventLevel.Informational)]
        public void RequestStart(string method, string? url)
        {
            Interlocked.Increment(ref _totalRequests);
            Interlocked.Increment(ref _currentRequests);
            WriteEvent(RequestStartId, method, url);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Event(RequestStopId, Level = EventLevel.Informational)]
        public void RequestStop()
        {
            Interlocked.Decrement(ref _currentRequests);
            WriteEvent(RequestStopId);
            _queueDuration?.WriteMetric(11);
        }

        [NonEvent]
        public void RequestFailed()
        {
            Interlocked.Increment(ref _failedRequests);
        }

        protected override void OnEventCommand(EventCommandEventArgs command)
        {
            if (command.Command == EventCommand.Enable)
            {
                // This is the convention for initializing counters in the RuntimeEventSource (lazily on the first enable command).
                // They aren't disabled afterwards...
                _requestsPerSecondCounter = new IncrementingPollingCounter("requests-per-second", this, () => _totalRequests)
                {
                    DisplayName = "Request Rate",
                    DisplayRateTimeScale = TimeSpan.FromSeconds(1)
                };

                _totalRequestsCounter = new PollingCounter("total-requests", this, () => _totalRequests)
                {
                    DisplayName = "Total Requests",
                };

                _currentRequestsCounter = new PollingCounter("current-requests", this, () => _currentRequests)
                {
                    DisplayName = "Current Requests"
                };

                _failedRequestsCounter = new PollingCounter("failed-requests", this, () => _failedRequests)
                {
                    DisplayName = "Failed Requests"
                };

                _queueDuration = new EventCounter("queue-duration", this)
                {
                    DisplayName = "Average Time in Queue",
                };
            }
        }

        [NonEvent]
        private unsafe void WriteEvent(int eventId, int arg1, int arg2, int arg3, string? arg4, string? arg5)
        {
            if (IsEnabled())
            {
                if (arg4 == null) arg4 = "";
                if (arg5 == null) arg5 = "";

                fixed (char* string4Bytes = arg4)
                fixed (char* string5Bytes = arg5)
                {
                    const int NumEventDatas = 5;
                    var descrs = stackalloc EventData[NumEventDatas];

                    descrs[0] = new EventData
                    {
                        DataPointer = (IntPtr)(&arg1),
                        Size = sizeof(int)
                    };
                    descrs[1] = new EventData
                    {
                        DataPointer = (IntPtr)(&arg2),
                        Size = sizeof(int)
                    };
                    descrs[2] = new EventData
                    {
                        DataPointer = (IntPtr)(&arg3),
                        Size = sizeof(int)
                    };
                    descrs[3] = new EventData
                    {
                        DataPointer = (IntPtr)string4Bytes,
                        Size = ((arg4.Length + 1) * 2)
                    };
                    descrs[4] = new EventData
                    {
                        DataPointer = (IntPtr)string5Bytes,
                        Size = ((arg5.Length + 1) * 2)
                    };

                    WriteEventCore(eventId, NumEventDatas, descrs);
                }
            }
        }
    }
}
