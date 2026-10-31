/**
 * Copyright 2020 The Nakama Authors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

var UnityWebGLSocketBridge = {
    $BRIDGE_NAME: {},
    $OPEN_METHOD_NAME: {},
    $CLOSE_METHOD_NAME: {},
    $MESSAGE_METHOD_NAME: {},
    $ERROR_METHOD_NAME: {},
    $SOCKET_REF_MAP: {},

    $INITIALIZE: function() {
        SOCKET_REF_MAP = new Map();
        BRIDGE_NAME = "[Nakama]";
        OPEN_METHOD_NAME = "NKBridgeOnOpen";
        CLOSE_METHOD_NAME = "NKBridgeOnClose";
        MESSAGE_METHOD_NAME = "NKBridgeOnMessage";
        ERROR_METHOD_NAME = "NKBridgeOnError";
    },

    NKCreateSocket: function(socketRef, addressPtr, timeoutMilliseconds) {
        if (!(SOCKET_REF_MAP instanceof Map)) {
            INITIALIZE();
        }

        if (SOCKET_REF_MAP.has(socketRef)) {
            SendMessage(BRIDGE_NAME, ERROR_METHOD_NAME, socketRef + "_" + "A WebSocket already exists for this reference.");
            return;
        }

        var address = UTF8ToString(addressPtr);
        var ws;
        try { ws = new WebSocket(address); }
        catch (error) {
            // Defer callback until C# has stored the returned socket reference.
            setTimeout(function() {
                SendMessage(BRIDGE_NAME, ERROR_METHOD_NAME, socketRef + "_" + "Could not open the matchmaking connection.");
            }, 0);
            return;
        }
        var timeout = setTimeout(function() {
            if (SOCKET_REF_MAP.get(socketRef) !== ws || ws.readyState !== WebSocket.CONNECTING) return;
            SOCKET_REF_MAP.delete(socketRef);
            ws.onopen = ws.onmessage = ws.onclose = ws.onerror = null;
            try { ws.close(); } catch (error) { }
            SendMessage(BRIDGE_NAME, ERROR_METHOD_NAME, socketRef + "_" + "The matchmaking connection timed out.");
        }, timeoutMilliseconds);
        ws.nakamaConnectTimeout = timeout;
        ws.onmessage = function(e) {
            if (SOCKET_REF_MAP.get(socketRef) !== ws) return;
            if (typeof e.data == 'string' && e.data.length <= 65536) {
                SendMessage(BRIDGE_NAME, MESSAGE_METHOD_NAME, socketRef + "_" + e.data);
            } else {
                clearTimeout(timeout);
                SOCKET_REF_MAP.delete(socketRef);
                try { ws.close(); } catch (error) { }
                SendMessage(BRIDGE_NAME, ERROR_METHOD_NAME, socketRef + "_" + "Received invalid data from remote.");
            }
          };
        ws.onopen = function(e) {
            clearTimeout(timeout);
            if (SOCKET_REF_MAP.get(socketRef) !== ws) return;
            SendMessage(BRIDGE_NAME, OPEN_METHOD_NAME, socketRef + "_");
        };
        ws.onclose = function(e) {
            clearTimeout(timeout);
            if (SOCKET_REF_MAP.get(socketRef) !== ws) return;
            SOCKET_REF_MAP.delete(socketRef);
            SendMessage(BRIDGE_NAME, CLOSE_METHOD_NAME, socketRef + "_" + e.code + "_" + e.reason);
        };
        ws.onerror = function(e) {
            clearTimeout(timeout);
            if (SOCKET_REF_MAP.get(socketRef) !== ws) return;
            SOCKET_REF_MAP.delete(socketRef);
            try { ws.close(); } catch (error) { }
            SendMessage(BRIDGE_NAME, ERROR_METHOD_NAME, socketRef + "_" + "The matchmaking connection failed.");
        };
        SOCKET_REF_MAP.set(socketRef, ws);
    },

    NKSocketState: function (socketRef) {
        if (!(SOCKET_REF_MAP instanceof Map)) {
            INITIALIZE();
        }

        if(!SOCKET_REF_MAP.has(socketRef)) {
            SendMessage(BRIDGE_NAME, ERROR_METHOD_NAME, socketRef + "_" + "Did not find websocket with given reference.");
            return -1;
        }
        return SOCKET_REF_MAP.get(socketRef).readyState;
    },

    NKCloseSocket: function (socketRef) {
        if (!(SOCKET_REF_MAP instanceof Map)) {
            INITIALIZE();
        }

        var ws = SOCKET_REF_MAP.get(socketRef);
        if (!ws) return;
        SOCKET_REF_MAP.delete(socketRef);
        clearTimeout(ws.nakamaConnectTimeout);
        ws.onopen = ws.onmessage = ws.onclose = ws.onerror = null;
        try { ws.close(); } catch (error) { }
    },

    NKSendData: function (socketRef, msg) {
        if (!(SOCKET_REF_MAP instanceof Map)) {
            INITIALIZE();
        }

        if(!SOCKET_REF_MAP.has(socketRef)) {
            SendMessage(BRIDGE_NAME, ERROR_METHOD_NAME, socketRef + "_" + "Did not find websocket with given reference.");
        } else {
            var data = UTF8ToString(msg);
            var ws = SOCKET_REF_MAP.get(socketRef);
            try {
                if (ws.readyState !== WebSocket.OPEN || data.length > 65536 || ws.bufferedAmount > 65536)
                    throw new Error('Unavailable');
                ws.send(data);
            } catch (error) {
                clearTimeout(ws.nakamaConnectTimeout);
                SOCKET_REF_MAP.delete(socketRef);
                try { ws.close(); } catch (closeError) { }
                SendMessage(BRIDGE_NAME, ERROR_METHOD_NAME, socketRef + "_" + "The matchmaking connection failed.");
            }
        }
    },
};

// Auto add to depends
autoAddDeps(UnityWebGLSocketBridge, '$INITIALIZE');
autoAddDeps(UnityWebGLSocketBridge, '$SOCKET_REF_MAP');
autoAddDeps(UnityWebGLSocketBridge, '$BRIDGE_NAME');
autoAddDeps(UnityWebGLSocketBridge, '$OPEN_METHOD_NAME');
autoAddDeps(UnityWebGLSocketBridge, '$CLOSE_METHOD_NAME');
autoAddDeps(UnityWebGLSocketBridge, '$MESSAGE_METHOD_NAME');
autoAddDeps(UnityWebGLSocketBridge, '$ERROR_METHOD_NAME');
mergeInto(LibraryManager.library, UnityWebGLSocketBridge);
