FROM node:22-alpine

WORKDIR /workspace/tools/device-log-collector

COPY server.js device-log-collector.config.json ./

EXPOSE 8765

CMD ["node", "server.js", "--host", "0.0.0.0", "--port", "8765"]
