
# EarthML Temply Headless

A small framework for generating images and run javascript headless for EarthML Temply.


## How it works

Using node and chrome headless, a websocket host and chrome instance is created. 

Using `RemotePage.initialize()` a remote page will connect to the websocket host and using events the `RemotePage` can controll the chrome instance and page for
[x] Reading File System files `"READ_FILE"`
[x] Write File System files `"WRITE_FILE"`
[x] File System files exist `"FILE_EXIST"`
[x] Render image from current browser view `"PAGE_RENDER"`
[x] Change viewport size `"CHANGE_VIEWPORT_SIZE"`

This makes it ideal for hosting simple webpages/javascript files that generate views and let them keep the resposibility of when to render images or writing files ect and removes the hasle of setting up the headless part.

## Examples

Will be coming


## Build 

### Powershell
```
dotnet  publish --framework netcoreapp2.0 -c Release -o out ; docker  build -t earthml/temply-headless .
```
### CMD
```
dotnet  publish --framework netcoreapp2.0 -c Release -o out && docker  build -t earthml/temply-headless .
```

## Run hello world
There is a small build in hello world example that can be run with ``--hello-world`


```
docker run -v c:\data\temply:/app/tmphost --rmearthml/temply-headless --hello-world
```

it will run a webserver serving the following page 
```
<html>
	<head>
		<script src=""node_modules/requirejs/require.js""></script>
	</head>
	<body>

	<script type="text/javascript">
		import * as require from "requirejs";
		
		require.config({
			paths:{
				'earthml-temply-headless':'node_modules/earthml-temply-headless/artifacts/src'
			}
		});

		import {RemotePage} from "earthml-temply-headless/remotepage/remotepage";

		let rp = new RemotePageModule.RemotePage(); 
		await rp.helloWorld();

		</script>

</body>
</html>
```



