#!/usr/bin/env node
var WebSocketServer = require('websocket').server;
var http = require('http');

var server = http.createServer(function(request, response) {
  console.log((new Date()) + ' Received request for ' + request.url);
  response.writeHead(404);
  response.end();
});
server.listen(8080, function() {
  console.log((new Date()) + ' Server is listening on port 8080');
});

wsServer = new WebSocketServer({
  httpServer: server,
  // You should not use autoAcceptConnections for production
  // applications, as it defeats all standard cross-origin protection
  // facilities built into the protocol and the browser.  You should
  // *always* verify the connection's origin and decide whether or not
  // to accept it.
  autoAcceptConnections: false
});

function originIsAllowed(origin) {
  // put logic here to detect whether the specified origin is allowed.
  return true;
}

function sendHeartMessage(connection, user) {
  var msg = '{' +
  '"id":"28888ee0-14fc-42cf-842e-559b70909b91",' +
  '"type":"heart",' +
  '"user":{' +
  '"id":"' + user.userId + '"' +
  '},' +
  '"color":"#F5A623"}';
  connection.sendUTF(msg);
  //console.log("heart | userId: " + user.userId);
}

function sendJoinMessage(connection, user) {
  var msg = '{' +
  '"id":"c9485103-ef4a-4c2e-a529-8cc39b1f9275",' +
  '"type":"join",' +
  '"user":{' +
  '"id":"' + user.userId + '",' +
  '"username":"' + user.username + '",' +
  '"display_name":"asdkjashd",' +
  '"profile_image_urls":[{"url":"https://pbs.twimg.com/profile_images/565642159492063232/ltKt547A_reasonably_small.jpeg"}],' +
  '"locale":"en",' +
  '"languages":["en"],' +
  '"superfan":true' +
  '},' +
  '"color":"#F5A623"}';

  connection.sendUTF(msg);
  //console.log("join  | userId: " + user.userId);
}

var mmm = 0;

function sendChatMessage(connection, user) {
  var msg = '{' +
  '"id":"c9485103-ef4a-4c2e-a529-8cc39b1f9275",' +
  '"type":"chat",' +
  '"text":"' + user.message + '",' +
  '"user":{' +
  '"id":"' + user.userId + '",' +
  '"username":"' + user.username + '",' +
  '"display_name":"asdkjashd",' +
  '"profile_image_urls":[{"url":"https://pbs.twimg.com/profile_images/565642159492063232/ltKt547A_reasonably_small.jpeg"}],' +
  '"locale":"en",' +
  '"languages":["en"],' +
  '"superfan":true' +
  '},' +
  '"color":"#F5A623"}';
  mmm++;
  connection.sendUTF(msg);
  //console.log("chat  | userId: " + user.userId);
}

function shuffle(a) {
  var j, x, i;
  for (i = a.length; i; i--) {
    j = randInt(i);
    x = a[i - 1];
    a[i - 1] = a[j];
    a[j] = x;
  }
}

function randInt(max) {
  return Math.floor(Math.random() * max)
}

function randText(length) {
  var text = "";
  var possible = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

  for( var i=0; i < length; i++ )
  text += possible.charAt(randInt(possible.length));

  return text;
}

function sendMessages(connection, interval, numJoins, numHearts, numChats) {
  var users = []
  for (var i=0; i < numUsers; i++) {
    users.push({
      'participantIndex': randInt(9999999),
      'userId': randText(13),
      'username': randText(randInt(12) + 1),
      'message': randText(randInt(25) + 10),
    });
  }
  var funcIds = {};
  var j = 0;
  var numMessagesPerUser = numJoins + numHearts + numChats;
  var numMessages = users.length * numMessagesPerUser;

  console.log("sending " + numMessages + " messages from " + users.length + " users");
  console.log("average qps: " + numMessages / (interval / 1000));

  var execOrder = [];
  for (var i=0; i<numMessages; i++) {
    execOrder.push(Math.floor(i/numMessagesPerUser));
  }
  shuffle(execOrder);

  for (var i=0; i<users.length; i++) {
    for (var k=0; k<numJoins; k++) {
      setTimeout(function () { sendJoinMessage(connection, users[execOrder[j]]); j++; }, randInt(interval));
    }
    for (var k=0; k<numHearts; k++) {
      setTimeout(function () { sendHeartMessage(connection, users[execOrder[j]]); j++; }, randInt(interval));
    }
    for (var k=0; k<numChats; k++) {
      setTimeout(function () { sendChatMessage(connection, users[execOrder[j]]); j++; }, randInt(interval));
    }
  }
}

// each user sends 1 join + 1 chat + numHearts hearts within interval
var numUsers = 40000;
var interval = 80000; // in milliseconds
var numJoins = 1;
var numHearts = 9;
var numChats = 1;

var intervals = {};

wsServer.on('request', function(request) {
  if (!originIsAllowed(request.origin)) {
    // Make sure we only accept requests from an allowed origin
    request.reject();
    console.log((new Date()) + ' Connection from origin ' + request.origin + ' rejected.');
    return;
  }

  var connection = request.accept(null, request.origin);
  console.log((new Date()) + ' Connection accepted.');
  connection.on('message', function(message) {
    if (message.type === 'utf8') {
      console.log('Received Message: ' + message.utf8Data);
    }
    else if (message.type === 'binary') {
      console.log('Received Binary Message of ' + message.binaryData.length + ' bytes');
    }
  });
  connection.on('close', function(reasonCode, description) {
    clearInterval(intervals[connection.remoteAddress]);
    console.log((new Date()) + ' Peer ' + connection.remoteAddress + ' disconnected.');
  });

  sendMessages(connection, interval, numJoins, numHearts, numChats);
  intervals[connection.remoteAddress] = setInterval(function () {
    sendMessages(connection, interval, numJoins, numHearts, numChats);
  }, interval);
});
