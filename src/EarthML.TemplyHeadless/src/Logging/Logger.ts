

function setValue(properties, key, value: object | string | number | boolean, serialize?) {
    if (typeof value === "object") {
        if (serialize) {
            properties[key] = JSON.stringify(value);
        } else {
            for (let propKey of Object.keys(value)) {
                setValue(properties, `${key}.${propKey}`, value[propKey]);
            }
        }

        return JSON.stringify(value);

    } else {
        return (properties[key] = value).toString();

    }



}

export class Logger {
    constructor() {

    }

    logInformation(msg: string, ...args: any[]) {
        let properties = { MessageTemplate: msg } as any;

        let renderedMsg = msg.replace(/\{(@?[a-zA-Z_\-^}]*)\}/g, (substr: string) => {
            let propName = substr.substr(1, substr.length - 2);
            let value = args.shift();
            if (typeof value !== "undefined") {
                if (propName.startsWith("@")) {
                    return setValue(properties, propName.substr(1), value);
                } else {
                    return setValue(properties, propName, value, true);
                }
            }
            return substr;
        });

        properties.RenderedMessage = renderedMsg;
        //console.log(properties);

     //   console.log([renderedMsg, properties]);
        console.log(renderedMsg);
    }
}