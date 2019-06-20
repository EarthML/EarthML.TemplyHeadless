import * as fs from "fs";
import * as http from "http";
import * as WebSocket from "websocket";
import * as puppeteer from "puppeteer";
import { Logger } from "../Logging/Logger";
import { log } from "util";
import { resolve } from "dns";
import * as fetch from "isomorphic-fetch";
import { settings } from "cluster";




export interface TemplyHeadlessOptions {
    webSocketHostPort?: number;
    chromeHeadlessArguments?: Array<string>
    headless?: boolean;
    size?: { width: number, height: number },
    outputPrefix: string;
    inputPrefix: string;
    data: any;
    url: string;
    headlessHost: string;
}

const defaultTemplyHeadlessOptions = {

    webSocketHostPort: 1337,
    chromeHeadlessArguments: ['--no-sandbox', '--disable-dev-shm-usage'],
    headless: true,
    size: { width: 1600, height: 1200 }
}



const parts = {};

function unwrapMessage(message: WebSocket.IMessage) {
    let event = JSON.parse(message.utf8Data);

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
        event.data = JSON.parse(parts[event.id].join(""));

    } else {
        console.log(event.type);
    }

    return event;
}

type EventsTypes = "CHANGE_VIEWPORT_SIZE" | "PAGE_RENDER";

const eventHandlers: { [key: string]: (event, page: puppeteer.Page, options: TemplyHeadlessOptions) => Promise<any> } = {
    "LOADED": (event, page, options) => {
        return new Promise((resolve, reject) => resolve(options.data));
    },
    "CHANGE_VIEWPORT_SIZE": async (event, page) => {
        console.log(JSON.stringify(event))
        await page.setViewport(event.data)

    },
    "INSTALL_DEPENDENCIES": async (event, page, options) => {
        console.log(event.data);
        console.log(`${options.headlessHost}/installDependencies`);
        try {
            let response = await fetch(`${options.headlessHost}/installDependencies`, { method: "POST", body: JSON.stringify(event.data) })
            console.log(response.ok);

            return response.ok;
        } catch (err) {
            console.log(err);
        }

    },
    "PAGE_RENDER": async (event, page, options) => {

        console.log(`Render image to ${options.outputPrefix + "/" + event.data.path}, page is closed: ${page.isClosed()}`);
        var result = await page.screenshot({ path: options.outputPrefix + "/" + event.data.path });

        console.log(`Rendered image to ${options.outputPrefix + "/" + event.data.path}: ${fs.existsSync(options.outputPrefix + "/" + event.data.path)} : ${(typeof result === "string" ? result : null)}`);
    },
    "FILE_EXIST": (event, page, options) => {
        return new Promise((resolve, reject) => {

            fs.exists(options.outputPrefix + event.data.path, (exists) => {
                resolve(exists);
            })

        });

    },
    "READ_FILE": (event, page, options) => {
        return new Promise((resolve, reject) => {
            fs.readFile(options.inputPrefix + "/" + event.data.path, event.data.encoding || 'utf8', (err, data) => {
                console.log(`Read file from ${event.data.path}: ${err}`);
                if (err) {
                    reject(err);
                } else {
                    resolve(data);
                }


            });
        });
    },
    "WRITE_FILE": (event, page, options) => {
        return new Promise((resolve, reject) => {
            fs.writeFile(options.outputPrefix + "/" + event.data.path, event.data.content, function (err) {
                if (err) {
                    reject(err);
                } else {
                    resolve(true);
                }
            });
        });
    }
}


function runRemotePage(wsServer, page, options: TemplyHeadlessOptions, logger) {
    return new Promise(async (resolve, reject) => {



        wsServer.on('request', function (request) {

            let connection = request.accept(null, request.origin);
            logger.logInformation("Websocket connection opened");

            connection.on('message', async function (message) {

                logger.logInformation("websocket message recieved :" + message.type);

                if (message.type === 'utf8') {
                    let event = unwrapMessage(message);

                    if (event) {
                        let result = undefined;

                        try {

                            if (event.type in eventHandlers) {
                                result = await eventHandlers[event.type](event, page, options);
                            }

                            if (event.id) {
                                let data = { id: event.id, data: result };
                                let msg = JSON.stringify(data);
                                if (msg.length > 65535) {
                                   // console.log("unwrapping message, " + msg.length);
                                    let partId = 0;
                                    msg = JSON.stringify(data);

                                    while (msg.length > 0) {
                                        let lng = Math.min(msg.length, 48000);
                                        let b = JSON.stringify({
                                            id: event.id,
                                            partId,
                                            part: msg.substr(0, lng),
                                            last: msg.length === lng
                                        });
                                     //   console.log("sending part " + msg.length);
                                        connection.send(b);
                                        msg = msg.substr(lng);
                                    }


                                } else {
                                    connection.send(msg);
                                }

                              //  connection.send(JSON.stringify({ id: event.id, data: result }));
                            }

                            if (event.type === "COMPLETE") {
                                if (event.data.err) {
                                    console.log("REJECTING COMPLETION");
                                    reject(event.data.err);
                                } else {
                                    console.log("ACCEPTING COMPLETION");
                                    console.log(event.data);
                                    if (options.headless) {
                                        resolve(event.data);
                                    }
                                }

                            }


                        } catch (err) {
                            console.log(`Failed to run eventhandler: ${event.type}, ${event.id} | ${err} `);
                            if (event.id) {
                                connection.send(JSON.stringify({ id: event.id, err: err.toString() }));
                            }
                        }



                    }

                }

            });

            connection.on('close', function (connection) {
                // close user connection
                console.log("HostRunner websocket connection closed");
                reject();
            });
        });
        logger.logInformation("Opening {url}", options.url);
        let response = await page.goto(options.url);
        if (response.ok) {

        } else {
            reject(await response.text());
        }


    });
}

/*
 * The main exported function for aspnet javascript services. 
 */
export default async function (options: TemplyHeadlessOptions, callback: (error, result) => void) {

    console.log(options);
    var logger = new Logger();
    options = Object.assign({}, defaultTemplyHeadlessOptions, options);
    console.log(options);
    try {

        process.on("uncaughtException", (e) => {
            console.error("Unhandled exeption:", e);
            process.exit(6);
        });
        process.on("unhandledRejection", (reason, p) => {
            console.error("Unhandled Rejection at: Promise", p, "reason:", reason);
            // application specific logging, throwing an error, or other logic here
            // cont = false;
        });






        await RunPage(options, logger, callback);





    } catch (err) {

        callback(err.toString(), null);
    }
}

async function RunPage(options: TemplyHeadlessOptions, logger: Logger, callback: (error: any, result: any) => void) {
    let server = http.createServer(function (request, response) {
        // process HTTP request. Since we're writing just WebSockets server
        // we don't have to implement anything.
    });
    server.listen(options.webSocketHostPort, function () { });

    // create the server
    let wsServer = new WebSocket.server({
        httpServer: server
    });

    logger.logInformation("EarthML.TemplyHeadless listening on {webSocketHostPort}", options.webSocketHostPort);


    let chromeInstance = await puppeteer.launch({
        headless: options.headless,
        args: options.chromeHeadlessArguments,
       // dumpio: true, //timeout : -1,
        //args: ["--no-sandbox"]

    }); // false starts browser

    logger.logInformation("ChromeInstance created with {headless} and arguments {@chromeHeadlessArguments}", options.headless, options.chromeHeadlessArguments);
    let page = await chromeInstance.newPage();
    page.on("close", (e) => {
        console.log("page closed: " + JSON.stringify(e));
    });
    chromeInstance.on("disconnected", (e) => {
        console.log("browser disconnected: " + JSON.stringify(e));
    });
    page.on('console', msg => console.log('PAGE LOG:', msg.text()));
    page.setViewport(options.size);
    logger.logInformation("Chrome Page created and viewport set to {@size}", options.size);
    try {
        let data = await runRemotePage(wsServer, page, options, logger);

        callback(null, data);
    }
    finally {
        await page.close();
        await chromeInstance.close();
        wsServer.closeAllConnections();

        server.close();
        server.removeAllListeners();
    }
}
