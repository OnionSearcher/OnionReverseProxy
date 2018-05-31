# OnionReverseProxy
Azure worker allowing any web site to work on Tor network
The worker will create one or more .onion address and forward the request to your website by using it's IP.

## Features :
- You can use several worker, and each worker can host several .onion address (but you can't share an onion address on several worker)
- The Web server can be what (IIS, Apache, Nginx..) and where (azure web site, azure VM, on premise server...) you like, as long it can be access by IP from the worker.
- The default setup will redirect http and https to the same web server, this can be tuned if required
- If you lready have hostname/private_key for .onion, just add them in the ExpertBundle/data/hsX folder as "copy always" files in order to keep them on deply

## Contributing

Requirement :

- Visual Studio Community 2017, with at last theses modules
    - ASP.Net and web development
    - Azure developement
- Download Tor Expert Bundle https://www.torproject.org/download/download.html.en to the folder ExpertBundle
