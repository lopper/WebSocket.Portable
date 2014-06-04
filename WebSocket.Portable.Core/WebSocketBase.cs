﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebSocket.Portable.Interfaces;
using WebSocket.Portable.Internal;
using WebSocket.Portable.Resources;
using WebSocket.Portable.Security;
using WebSocket.Portable.Tasks;

namespace WebSocket.Portable
{
    public abstract class WebSocketBase : ICanLog, IWebSocket
    {        
        private bool _isSecure;
        private Uri _uri;
        private WebSocketCompression _compression;
        private int _state;
        private ITcpConnection _tcp;

        /// <summary>
        /// Prevents a default instance of the <see cref="WebSocketBase"/> class from being created.
        /// </summary>
        protected WebSocketBase()
        {
            _compression = WebSocketCompression.None;
            _state = WebSocketState.Closed;
        }

        public Task CloseAsync(WebSocketErrorCode errorCode)
        {


            return TaskAsyncHelper.Empty;
        }

        /// <summary>
        /// Connects asynchronous.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <returns></returns>
        public Task ConnectAsync(string uri)
        {
            return this.ConnectAsync(uri, CancellationToken.None);
        }

        /// <summary>
        /// Connects asynchronous.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.InvalidOperationException">Cannot connect because current state is  + _state</exception>
        public async Task ConnectAsync(string uri, CancellationToken cancellationToken)
        {
            var oldState = Interlocked.CompareExchange(ref _state, WebSocketState.Connecting, WebSocketState.Closed);
            if (oldState != WebSocketState.Closed)
                throw new InvalidOperationException(ErrorMessages.InvalidState + _state);

            if (uri == null)
                throw new ArgumentNullException("uri");

            _uri = WebSocketHelper.CreateWebSocketUri(uri);
            _isSecure = _uri.Scheme == "wss";
            if (_isSecure)
                throw new NotSupportedException(ErrorMessages.SecureConnectionsAreNotYetSupported);

            _tcp = await this.ConnectAsync(_uri.DnsSafeHost, _uri.Port, cancellationToken);
            Interlocked.Exchange(ref _state, WebSocketState.Connected);
        }

        /// <summary>
        /// Connects asynchronous.
        /// </summary>
        /// <param name="host">The host.</param>
        /// <param name="port">The port.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        protected abstract Task<ITcpConnection> ConnectAsync(string host, int port, CancellationToken cancellationToken);

        /// <summary>
        /// Sends the default handshake asynchronous.
        /// </summary>
        /// <returns></returns>
        public Task<WebSocketResponseHandshake> SendHandshakeAsync()
        {
            return this.SendHandshakeAsync(CancellationToken.None);
        }

        /// <summary>
        /// Sends the default handshake asynchronous.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public Task<WebSocketResponseHandshake> SendHandshakeAsync(CancellationToken cancellationToken)
        {
            return this.SendHandshakeAsync(new WebSocketRequestHandshake(_uri), cancellationToken);
        }

        /// <summary>
        /// Sends the handshake asynchronous.
        /// </summary>
        /// <param name="handshake">The handshake.</param>
        /// <returns></returns>
        public Task<WebSocketResponseHandshake> SendHandshakeAsync(WebSocketRequestHandshake handshake)
        {
            return this.SendHandshakeAsync(handshake, CancellationToken.None);
        }

        /// <summary>
        /// Sends the handshake asynchronous.
        /// </summary>
        /// <param name="handshake">The handshake.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public async Task<WebSocketResponseHandshake> SendHandshakeAsync(WebSocketRequestHandshake handshake, CancellationToken cancellationToken)
        {
            var oldState = Interlocked.CompareExchange(ref _state, WebSocketState.Opening, WebSocketState.Connected);
            if (oldState != WebSocketState.Connected)
                throw new InvalidOperationException(ErrorMessages.InvalidState + _state);

            var data = handshake.ToString();
            await this.SendAsync(data, Encoding.UTF8, cancellationToken);

            var responseHeaders = new List<string>();
            var line = await _tcp.ReadLineAsync(cancellationToken);
            while (!String.IsNullOrEmpty(line))
            {
                responseHeaders.Add(line);
                line = await _tcp.ReadLineAsync(cancellationToken);
            }

            var response = WebSocketResponseHandshake.Parse(responseHeaders);
            if (response.StatusCode != HttpStatusCode.SwitchingProtocols)
            {
                var versions = response.SecWebSocketVersion;
                if (versions != null && !versions.Intersect(Consts.SupportedClientVersions).Any())
                    throw new WebSocketException(WebSocketErrorCode.HandshakeVersionNotSupported);                    
                    
                throw new WebSocketException(WebSocketErrorCode.HandshakeInvalidStatusCode);
            }

            var challenge = Encoding.UTF8.GetBytes(handshake.SecWebSocketKey + Consts.ServerGuid);
            var hash = Sha1Digest.ComputeHash(challenge);
            var calculatedAccept = Convert.ToBase64String(hash);

            if (response.SecWebSocketAccept != calculatedAccept)
                throw new WebSocketException(WebSocketErrorCode.HandshakeInvalidSecWebSocketAccept);       

            response.RequestMessage = handshake;

            Interlocked.Exchange(ref _state, WebSocketState.Open);

            return response;
        }

        /// <summary>
        /// Sends data asynchronous.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="encoding">The encoding.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        private Task SendAsync(string data, Encoding encoding, CancellationToken cancellationToken)
        {
            var bytes = encoding.GetBytes(data);
            return this.SendAsync(bytes, 0, bytes.Length, cancellationToken);
        }

        /// <summary>
        /// Sends data asynchronous.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        private Task SendAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
        {
            return _tcp.WriteAsync(buffer, offset, length, cancellationToken);
        }

        public void Dispose()
        {
            // TODO
        }
    }
}
