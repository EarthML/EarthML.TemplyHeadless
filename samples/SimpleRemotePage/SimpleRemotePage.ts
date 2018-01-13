
import { RemotePage } from "earthml-temply-headless/RemotePage/RemotePage";

let a = new RemotePage();


export default class MyRemotePage extends RemotePage {

    async run() {

        //Do anything, like 

        let h1 = document.createElement("h1");
        h1.innerText = "Simple Remote Page";
        window.document.body.appendChild(h1);

        await this.renderImage();

        await this.complete({status:"succeded"});

    }        
}

            