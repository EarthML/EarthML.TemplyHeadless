# Simple Remote Page:



The Remote Page
```

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

```

and hosting this on a webserver:
```
  public class Program
    {
        public static Task Main(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .Configure(app =>
                {
                    app.Run(async (context) =>
                    {
                        if (context.Request.Path == "/SimpleRemotePage.js")
                        {
                            await context.Response.SendFileAsync("SimpleRemotePage.js");
                        }
                    });
              })
              .Build()
            .RunAsync();
      
    }
}
```

and hosted on azure websites: http://simpleremotepage20180113011210.azurewebsites.net/SimpleRemotePage.js

## Run the remote page

We are mapping a folder to /app/tmphost to reuse npm moduels across calls and an output folder where render image calls are stored.

Running the following command will render our simple remote page above.

```
docker run -v c:\data\temply:/app/tmphost -v c:\data\temply-out:/app/output --rm earthml/temply-headless --remote-page-main SimpleRemotePage --remote-page-location http://simpleremotepage20180113011210.azurewebsites.net
```






