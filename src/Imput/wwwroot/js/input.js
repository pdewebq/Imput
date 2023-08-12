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

connection.on("ReceiveKey", (keyCode, keyState, _nativeKeyCode) => {
    const keyElems = document.querySelectorAll(`[data-key-code="${keyCode}"]`)
    for (const keyElem of keyElems) {
        keyElem.setAttribute("data-key-state", keyState)
    }
})

connection.start()
