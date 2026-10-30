mergeInto(LibraryManager.library, {
  $FrontierBrowserSockets: {},

  FrontierBrowserCreate__deps: ['$FrontierBrowserSockets'],
  FrontierBrowserCreate: function(reference, receiverPtr, urlPtr, maxMessage, maxBuffered) {
    if (FrontierBrowserSockets[reference]) return 0;
    var receiver = UTF8ToString(receiverPtr);
    var socket;
    try { socket = new WebSocket(UTF8ToString(urlPtr)); }
    catch (error) { return 0; }
    var entry = { socket: socket, receiver: receiver, maxMessage: maxMessage, maxBuffered: maxBuffered };
    FrontierBrowserSockets[reference] = entry;
    socket.binaryType = 'arraybuffer';
    function active() { return FrontierBrowserSockets[reference] === entry; }
    socket.onopen = function() {
      if (active()) SendMessage(receiver, 'BrowserSocketOpened', '');
    };
    socket.onmessage = function(event) {
      if (!active()) return;
      if (!(event.data instanceof ArrayBuffer) || event.data.byteLength < 2 || event.data.byteLength > maxMessage) {
        SendMessage(receiver, 'BrowserSocketFailed', '');
        return;
      }
      var bytes = new Uint8Array(event.data);
      var text = '';
      for (var i = 0; i < bytes.length; i++) text += String.fromCharCode(bytes[i]);
      SendMessage(receiver, 'BrowserSocketMessage', btoa(text));
    };
    socket.onerror = function() {
      if (active()) SendMessage(receiver, 'BrowserSocketFailed', '');
    };
    socket.onclose = function() {
      if (!active()) return;
      delete FrontierBrowserSockets[reference];
      SendMessage(receiver, 'BrowserSocketClosed', '');
    };
    return 1;
  },

  FrontierBrowserSend__deps: ['$FrontierBrowserSockets'],
  FrontierBrowserSend: function(reference, bytesPtr, length) {
    var entry = FrontierBrowserSockets[reference];
    if (!entry || entry.socket.readyState !== WebSocket.OPEN || length < 2 || length > entry.maxMessage
        || entry.socket.bufferedAmount + length > entry.maxBuffered) return 0;
    try {
      entry.socket.send(HEAPU8.subarray(bytesPtr, bytesPtr + length));
      return 1;
    } catch (error) { return 0; }
  },

  FrontierBrowserClose__deps: ['$FrontierBrowserSockets'],
  FrontierBrowserClose: function(reference) {
    var entry = FrontierBrowserSockets[reference];
    if (!entry) return;
    delete FrontierBrowserSockets[reference];
    var socket = entry.socket;
    socket.onopen = socket.onmessage = socket.onclose = socket.onerror = null;
    try { socket.close(1000, 'Client left.'); } catch (error) { }
  }
});
