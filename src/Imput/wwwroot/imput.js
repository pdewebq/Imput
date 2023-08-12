import "https://cdn.jsdelivr.net/npm/@microsoft/signalr@7.0.9/dist/browser/signalr.min.js"

function getAllKeyElements() {
    return document.querySelectorAll("[data-key-code]")
}

function upAllKeys() {
    getAllKeyElements().forEach((elem) => {
        elem.setAttribute("data-key-state", "up")
    })
}

upAllKeys()

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/input")
    .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: retryContext => {
            if (retryContext.previousRetryCount <= 10) {
                return 1_000
            } else {
                return 4_000
            }
        }
    })
    .build()

connection.onreconnected((_connectionId) => {
    upAllKeys()
})

connection.on("ReceiveKey", (keyCode, keyState) => {
    const keyElems = document.querySelectorAll(`[data-key-code="${keyCode}"]`)
    for (const keyElem of keyElems) {
        keyElem.setAttribute("data-key-state", keyState)
    }
})

async function start() {
    try {
        await connection.start()
        console.assert(connection.state === signalR.HubConnectionState.Connected)
    } catch (err) {
        console.assert(connection.state === signalR.HubConnectionState.Disconnected)
        console.error(err)
        setTimeout(() => start(), 5_000)
    }
}

start()
