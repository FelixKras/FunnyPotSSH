#!/bin/bash
docker rm -f funnypot-container 2>/dev/null
docker-compose up -d --build
