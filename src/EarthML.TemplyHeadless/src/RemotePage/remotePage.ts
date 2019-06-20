


import { broadcastEvent } from "./boardcastEvent";
import { Deferred } from "./Deferred";
import { error } from "util";


interface NodeRequire {
    config(c: any);
}
declare var requirejs;


const parts = {};

function unwrapMessage(message: MessageEvent) {
    let event = JSON.parse(message.data);
    console.log("recieved message" + typeof (message.data) + " " + typeof (event.partId));
  

    if ("partId" in event) {
        
        console.log(`Recieved part ${event.partId} ${event.part.length}`);
        if (!(event.id in parts)) {
            parts[event.id] = [];
        }
        parts[event.id].push(event.part);

        if (!event.last) {
            return;
        }
        console.log(`Recieved last part ${event.partId} ${event.part.length}`);
       // event.data = JSON.parse(parts[event.id].join(""));
        var str = parts[event.id].join("");
        delete parts[event.id];
        return JSON.parse(str);

    } else {
        console.log(event.type);
    }

    return event;
}

export class RemotePage {

    config: any;
    connection: WebSocket = null;
    tasks: { [key: string]: Deferred } = {};

    constructor() {

    }

    async run() {

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
        console.log("completing1");
        return broadcastEvent(this.connection, "COMPLETE", data, this.tasks);
    }

    renderImage(path:string="test.jpg") {
        return broadcastEvent(this.connection, "PAGE_RENDER", { path }, this.tasks);
    }

    installDependencies(dps) {
        return broadcastEvent(this.connection, "INSTALL_DEPENDENCIES", dps, this.tasks);
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
                this.config = extraConfig;
                console.log(JSON.stringify(extraConfig));
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
                console.log(message);
                try {

                    var json = unwrapMessage(message);
                    if (json) {
                        //var json = JSON.parse(message.data);

                       
                        if (json.id in this.tasks) {
                            if ("err" in json) {
                                this.tasks[json.id].reject(json.err);
                            } else {
                                this.tasks[json.id].resolve(json.data);
                            }

                        }
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
