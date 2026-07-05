const express = require('express');
const http = require('http');
const { Server } = require('socket.io');
const cors = require('cors');

const app = express();
app.use(cors());

const server = http.createServer(app);
const io = new Server(server, {
  cors: {
    origin: "*",
    methods: ["GET", "POST"]
  }
});

// Almacenar en qué sala está cada usuario
const userRooms = {};

io.on('connection', (socket) => {
  console.log(`Usuario conectado: ${socket.id}`);

  // Unirse a una sala de transmisión (con un código, por ejemplo: 1234)
  socket.on('join-room', (roomId) => {
    socket.join(roomId);
    userRooms[socket.id] = roomId;
    console.log(`Socket ${socket.id} se unió a la sala ${roomId}`);
    
    // Notificar a los demás en la sala que alguien nuevo se conectó
    socket.to(roomId).emit('user-joined', socket.id);
  });

  // Intercambio de ofertas WebRTC
  socket.on('offer', (payload) => {
    const roomId = userRooms[socket.id];
    console.log(`Transmitiendo oferta en la sala ${roomId}`);
    // Enviar oferta a todos en la sala menos al emisor
    socket.to(roomId).emit('offer', payload);
  });

  // Intercambio de respuestas WebRTC
  socket.on('answer', (payload) => {
    const roomId = userRooms[socket.id];
    console.log(`Transmitiendo respuesta en la sala ${roomId}`);
    socket.to(roomId).emit('answer', payload);
  });

  // Intercambio de candidatos ICE (para la conexión P2P)
  socket.on('ice-candidate', (payload) => {
    const roomId = userRooms[socket.id];
    socket.to(roomId).emit('ice-candidate', payload);
  });

  socket.on('disconnect', () => {
    const roomId = userRooms[socket.id];
    console.log(`Usuario desconectado: ${socket.id}`);
    if (roomId) {
      socket.to(roomId).emit('user-disconnected', socket.id);
      delete userRooms[socket.id];
    }
  });
});

const PORT = process.env.PORT || 3000;
server.listen(PORT, () => {
  console.log(`Servidor de señalización corriendo en el puerto ${PORT}`);
});
