


import { broadcastEvent } from "./boardcastEvent";
import { Deferred } from "./Deferred";


interface NodeRequire {
    config(c: any);
}
declare var requirejs;

export class RemotePage {

    connection: WebSocket = null;
    tasks: { [key: string]: Deferred } = {};

    constructor(config?, connection?, tasks?) {

    }

    async helloWorld() {
        await this.initialize();

        let h1 = document.createElement("h1");
        h1.innerText = "Hello World";
        window.document.body.appendChild(h1);

        await this.renderImage();
        await this.complete({ "hello": "world" });
    }

    complete(data) {
        console.log("completing");
        return broadcastEvent(this.connection, "COMPLETE", data, this.tasks);
    }

    renderImage() {
        return broadcastEvent(this.connection, "PAGE_RENDER", { path: "test.jpg" }, this.tasks);
    }

    initialize() {
        return new Promise(async (resolve, reject) => {
            this.connection = new WebSocket('ws://127.0.0.1:1337');
            this.connection.onopen = async () => {
                console.log("Opened");
                requirejs.config({
                    paths: {
                        "es6-promise": "/libs/es6-promise/es6-promise.min"
                    }
                });

                let extraConfig = await broadcastEvent(this.connection, "LOADED", { dependencies: { "es6-promise": "es6-promise/dist" } }, this.tasks);
                console.log(extraConfig);
               // requirejs.config(extraConfig);

                resolve(extraConfig);
                //let obj = await send loaded; 
                //connection.send(JSON.stringify({
                //    type: "LOADED",
                //    dependencies: { "es6-promise": "es6-promise/dist" }
                // }));
            };

            this.connection.onerror = function (error) {
                console.log("FAILED TO OPEN SOCKET");
                reject();
                // an error occurred when sending/receiving data
            };

            this.connection.onmessage = (message)=> {
                // try to decode json (I assume that each message from server is json)
                try {
                    var json = JSON.parse(message.data);
                    if (json.id in this.tasks) {
                        this.tasks[json.id].resolve(json.data);
                    }
                } catch (e) {
                    console.log('This doesn\'t look like a valid JSON: ', message.data);
                    return;
                }
                // handle incoming message
            };
        });
    }
}
