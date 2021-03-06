﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation;
using Windows.Foundation.Collections;
using LoopyVideo.Logging;

namespace LoopyVideo.Commands
{
    /// <summary>
    /// Message Recieved event handler
    /// </summary>
    /// <param name="command">The command recieved</param>
    /// <returns>The current status </returns>
    public delegate LoopyCommand ReceiveMessage(LoopyCommand command);

    /// <summary>
    /// The connection between LoopyVideo.AppService and  the apps being serviced
    /// </summary>
    public sealed class AppConnection : IDisposable
    {
        private Logger _log;

        private readonly static string _serviceNameDefault = "net.manipulatormanor.LoopyWebServer";
        private readonly static string _serviceFamilyNameDefault = "LoopyVideo.WebService-uwp_n1q2psqd6svm2";

        public event ReceiveMessage MessageReceived;


        // The instance of this class

        private string _serviceName;
        public string ServiceName
        {
            get
            {
                if(string.IsNullOrEmpty(_serviceName))
                {
                    _serviceName = _serviceNameDefault;
                }
                return _serviceName;
            }
            set { _serviceName = value; }
        }

        private string _familyName;
        public string ServiceFamilyName
        {
            get
            {
                if (string.IsNullOrEmpty(_familyName))
                {
                    _familyName = _serviceFamilyNameDefault;
                }
                return _familyName;
            }
            set { _familyName = value; }
        }

        private object _connectionLock = new object();    
        private AppServiceConnection _connection;
        public AppServiceConnection Connection
        {
            get { return _connection; }
            set
            {
                lock (_connectionLock)
                {
                    if(_connection != null)
                    {
                        _connection.RequestReceived -= RequestReceived;
                        _connection.ServiceClosed -= ServiceClosed;
                        _connection.Dispose();
                    }
                    _connection = value;
                    if (_connection != null)
                    {
                        _connection.RequestReceived += RequestReceived;
                        _connection.ServiceClosed += ServiceClosed;
                        Status = AppServiceConnectionStatus.Success;
                    }
                }
            }
        }

 
        public AppServiceConnectionStatus Status
        {
            get;
            private set;
        }

        public bool IsValid()
        {
            _log.Information($"AppConnection.IsValid: {((null != Connection) ? "is Connected" : "is Not connected}")} and Status : {Status.ToString()}");
            return (null != Connection) && (Status == AppServiceConnectionStatus.Success);
        }


        /// <summary>
        /// Default constructor
        /// </summary>
        public AppConnection() : this("AppConnection")
        {
        }

        public AppConnection(string logProviderName)
        {
            _log = new Logger(logProviderName);
            _connection = null;
            Status = AppServiceConnectionStatus.Unknown;
        }

        ~AppConnection()
        {
            Dispose(true);
        }

        #region IDisposable Members

        /// <summary>
        /// Internal variable which checks if Dispose has already been called
        /// </summary>
        private Boolean disposed;

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(Boolean disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                Connection = null;
                if (_log != null)
                {
                    _log.Dispose();
                }
            }

            disposed = true;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            // Call the private Dispose(bool) helper and indicate 
            // that we are explicitly disposing
            this.Dispose(true);

            // Tell the garbage collector that the object doesn't require any
            // cleanup when collected since Dispose was called explicitly.
            GC.SuppressFinalize(this);
        }

        #endregion

        public IAsyncOperation<AppServiceConnectionStatus> OpenConnectionAsync()
        {
            _log.Information("OpenConnectionAsync: called");
            return Task<AppServiceConnectionStatus>.Run(async () =>
            {
                _log.Information("OpenConnectionAsync: Creating App Service Connection");
                AppServiceConnection connection = new AppServiceConnection();

                // Here, we use the app service name defined in the app service provider's Package.appxmanifest file in the <Extension> section.
                connection.AppServiceName = ServiceName;

                // Use Windows.ApplicationModel.Package.Current.Id.FamilyName within the app service provider to get this value.
                connection.PackageFamilyName = ServiceFamilyName;

                Status = await connection.OpenAsync();
                bool bRet = Status == AppServiceConnectionStatus.Success;
                _log.Information($"OpenConnectionAsync: Connection Status = {Status.ToString()}");

                if (bRet)
                {
                    Connection = connection;
                }
                return Status;
            }).AsAsyncOperation<AppServiceConnectionStatus>();
        }

        /// <summary>
        /// Send a Command message to the head applicaiton
        /// </summary>
        /// <param name="command">The command to send</param>
        /// <returns>The LoopyCommand containing the response</returns>
        public IAsyncOperation<LoopyCommand> SendCommandAsync(LoopyCommand command)
        {

            return Task<LoopyCommand>.Run(async () =>
           {
               AppServiceResponse response = null;
               LoopyCommand retCommand;
               if (IsValid())
               {
                   _log.Information($"SendCommandAsync: Sending {command.ToString()}");
                   response = await Connection.SendMessageAsync(command.ToValueSet());
                   if(response.Status != AppServiceResponseStatus.Success)
                   {
                       retCommand = new LoopyCommand(
                                                    CommandType.Error,
                                                    $"Command response status: {response.Status} with message: {ValueSetOut.ToString(response.Message)}"
                                                    );
                       _log.Error(retCommand.Param.ToString());  
                   }
                   else
                   {
                       retCommand = LoopyCommand.FromValueSet(response.Message);
                   }

               }
               else
               {
                   _log.Error("SendCommandAsync: called before the connection is valid");
                   throw new InvalidOperationException("Cannot send a command until connection is opened");
               }
               return retCommand;
           }
            ).AsAsyncOperation<LoopyCommand>();
        }


        /// <summary>
        /// Receive messages from the other app
        /// </summary>
        /// <param name="sender">The connection the message is from</param>
        /// <param name="args">The argurments fo the message</param>
        private async void RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            if(!IsValid())
            {
                _log.Information("SendCommandAsync: called before the connection is valid");
                throw new InvalidOperationException("Message recieved before connection is opened");
            }
            var requestDefferal = args.GetDeferral();
            try
            {

                _log.Information($"AppConnection.RequestReceived: received the following message: {ValueSetOut.ToString(args.Request.Message)}");
                AppServiceResponseStatus status = AppServiceResponseStatus.Unknown;
                if (MessageReceived != null)
                {
                    LoopyCommand lc = LoopyCommand.FromValueSet(args.Request.Message);
                    LoopyCommand result = MessageReceived(lc);
                    _log.Information($"AppConnection.RequestRecieved: response: {result.ToString()}");
                    status = await args.Request.SendResponseAsync(result.ToValueSet());
                }

                _log.Information($"Sending Response to Request returned: {status.ToString()}");
            }
            finally
            {
                requestDefferal.Complete();
            }
        }

        private void ServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            _log.Information($"AppService Connection has be closed by: {args.Status.ToString()}");
            Connection = null;
            Status = AppServiceConnectionStatus.Unknown;
        }

    }
}
