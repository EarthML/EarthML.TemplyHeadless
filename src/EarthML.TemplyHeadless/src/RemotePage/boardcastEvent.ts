import { generateUUID } from "./generateUUID"; 
import { Deferred } from "./Deferred";

export function broadcastEvent(connection: WebSocket, type: string, data: any, tasks: { [key: string]: Deferred }) {
    let id = generateUUID();
   
    let task = {
        id: id,
        type: type,
        data: data,

    };
    let msg = JSON.stringify(task);

    if (msg.length > 65535) {
         
        let partId = 0;
        msg = JSON.stringify(data);

        while (msg.length > 0) {
            let lng = Math.min(msg.length, 48000);
            let b = JSON.stringify({
                id,
                type,
                partId,
                part: msg.substr(0, lng),
                last: msg.length === lng
            });
            
            connection.send(b);
            msg = msg.substr(lng);
        }


    } else {
        connection.send(msg);
    }
    console.log(msg)
    tasks[id] = new Deferred();
    return tasks[id].promise;
}