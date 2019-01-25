
import { RemotePage } from "earthml-temply-headless/RemotePage/RemotePage";
 


function getData(appId, appKey, query) {
    return new Promise<c3.Data>((resolve, reject) => {
        let hr = new XMLHttpRequest();
        hr.onreadystatechange = () => {
            if (hr.readyState == XMLHttpRequest.DONE) {
                let data = JSON.parse(hr.responseText);
                console.log(data);

                let rows = data["Tables"][0]["Rows"].sort((a, b) => new Date(a[0]).getTime() - new Date(b[0]).getTime());
                console.log(rows);                                  
                // let columns = data["Tables"][0]["Columns"].map((c:any) => c["ColumnName"]);
                let myX = rows.map((row: [string, number]) => row[0]) as Array<number | string>;
                let myY = rows.map((row: [string, number]) => row[1]) as Array<number | string>;
              //  recordings.unshift("recordings");
               // time.unshift("x");

               // let myX = ["2018-01-13T07:00:00Z", "2015-11-21", "2015-11-22", "2015-11-23", "2015-11-24"];
              //  let myY = [1, 2, 3, 4, 5];
                myX.splice(0, 0, 'x');
                myY.splice(0, 0, 'Recordings');
                resolve({
                    x:'x',
                    xFormat: '%Y-%m-%dT%H:%M:%SZ',
                    columns: [
                        myX,
                        myY                   
                        
                    ],
                    type: 'bar'
                    //  done: 
                });



            }
        }
        hr.open('POST', `https://api.applicationinsights.io/beta/apps/${appId}/query`, true);
        hr.setRequestHeader("x-api-key", appKey);
        hr.setRequestHeader("content-type", "application/json");
        hr.send(JSON.stringify({ "csl": "set truncationmaxrecords = 10000; set truncationmaxsize = 67108864; " + query }));
    });
}
 

export default class ApplicationInsightAnalyticRemotePage extends RemotePage {
    

    async run() {

        //Do anything, like 
       
        let installed = await super.installDependencies({
            dependencies: {
                "c3": "0.4.11",
                "d3": "^3.5.17",
                "require-css":"latest"
            },
            npmCopy: {
                "c3": ["d3/d3.min.js", "c3/c3.min.js", "c3/c3.min.css"]
            }
        });

        requirejs.config({
            shim: {
                "c3": {
                    deps: ["d3", "css!/node_modules/c3/c3.min.css"]
                },  
            },
            paths: {
                "css": "node_modules/require-css/css",
                "c3": "node_modules/c3/c3.min",
                "d3":"node_modules/d3/d3.min",
            }
        });


        let c3 = await import("c3");
        let div = document.createElement("div");
        div.id = "chart";
        div.style.height = "100%";
        div.style.width = "100%"
        document.body.appendChild(div);

        let interval = 1;
      
        let data = await getData("", "", "requests | where timestamp > now(-1d) and name == 'POST RecordingV2/CreateRecord [flowId/subscriptionId]' | summarize event_count = count() by bin(timestamp, " + interval+"h) | render barchart");

        let first = new Date(data.columns[0][1] as string);
        let last = new Date(data.columns[0][data.columns[0].length - 1] as string);
        let values = [];
        while (first.getTime() < last.getTime()) {
            values.push(first.toISOString().split('.')[0] + "Z");
            first = new Date(first.getTime() + interval * 3600 * 1000);
        }
        values.push(last.toISOString().split('.')[0] + "Z");
        console.log(values);

        var chart = c3.generate({
            bindto: '#chart',
            data: data,
            axis: {
                x: {
                    type: 'timeseries',
                    tick: {
                      //  fit:false,
                      
                        rotate: 45,
                       // count: data.columns[0].length,
                        values: values, // this should be same format with x
                        format: "%m-%d %H:%M"//format in which you want the output
                    } as any
                },

            },
            bar: {
                width: {
                    ratio: 0.95 // this makes bar width 50% of length between ticks
                }
                // or
                //width: 100 // this makes bar width 100px
            }
        });


        await this.renderImage("recording.jpg");
        


        await this.complete(data);

    }
}

