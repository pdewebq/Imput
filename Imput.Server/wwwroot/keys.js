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
        const [keyAction, keyCodeStr] = event.data.split(",")
        const keyCode = parseInt(keyCodeStr)

        const elems = document.querySelectorAll(`[data-keycode="${keyCode}"]`)
        for (const elem of elems) {
            if (keyAction === "up") {
                elem.classList.add("is-up")
                elem.classList.remove("is-down")
            } else if (keyAction === "down") {
                elem.classList.remove("is-up")
                elem.classList.add("is-down")
            }
        }
    }
}

connect()
