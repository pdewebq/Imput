document.querySelectorAll("[data-key-code]").forEach((elem) => {
    elem.setAttribute("data-key-state", "up")
})

function connect() {
    const webSocket = new WebSocket(`ws://${window.location.host}/ws/keys`)

    webSocket.onerror = (event) => {
        console.error("WebSocket error")
    }

    webSocket.onopen = (event) => {
        console.log('WebSocket connected')
    }

    webSocket.onclose = (event) => {
        console.log('WebSocket is closed. Reconnect will be attempted in 1 second. ', { code: event.code, reason: event.reason, wasClean: event.wasClean})
        setTimeout(() => {
            connect()
        }, 1_000)
    }

    webSocket.onmessage = (event) => {
        // console.log(event.data)
        const keyEvent = JSON.parse(event.data)

        const elems = document.querySelectorAll(`[data-key-code="${keyEvent.code}"]`)
        for (const elem of elems) {
            elem.setAttribute("data-key-state", keyEvent.keyAction)
        }
    }
}

connect()
