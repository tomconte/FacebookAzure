# A sample Facebook application for Windows Azure

This sample implements a basic architecture for a scalable Facebook application:


- Work is split between Web and Worker roles
- Each role can be scaled out independantly
- Work items are handled asynchronously using Queues
- Table Storage & Blob Storage are used for data storage
- JSON Blobs are used to cache ready-to-use data


