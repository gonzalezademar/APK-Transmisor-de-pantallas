import React, { useState, useEffect, useRef } from 'react';
import {
  SafeAreaView,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
  TextInput,
  StatusBar,
} from 'react-native';
import { io, Socket } from 'socket.io-client';
import { 
  RTCPeerConnection, 
  RTCSessionDescription, 
  RTCIceCandidate, 
  mediaDevices, 
  RTCView, 
  MediaStream 
} from 'react-native-webrtc';

const SERVER_URL = 'http://192.168.1.100:3000'; // Importante: el usuario deberá poner aquí la IP real de su servidor Node

const configuration = {
  iceServers: [
    { urls: 'stun:stun.l.google.com:19302' },
    { urls: 'stun:stun1.l.google.com:19302' }
  ]
};

function App(): React.JSX.Element {
  const [mode, setMode] = useState<'HOME' | 'TRANSMITTER' | 'RECEIVER'>('HOME');
  const [roomId, setRoomId] = useState('1234');
  const [socket, setSocket] = useState<Socket | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const [remoteStream, setRemoteStream] = useState<MediaStream | null>(null);
  
  const peerConnectionRef = useRef<RTCPeerConnection | null>(null);
  const localStreamRef = useRef<MediaStream | null>(null);

  useEffect(() => {
    const newSocket = io(SERVER_URL);
    setSocket(newSocket);

    newSocket.on('connect', () => {
      console.log('Conectado al servidor:', newSocket.id);
      setIsConnected(true);
    });

    return () => {
      newSocket.disconnect();
    };
  }, []);

  // Manejador de señalización de WebRTC
  useEffect(() => {
    if (!socket) return;

    socket.on('user-joined', async () => {
      if (mode === 'TRANSMITTER') {
        // Alguien se unió a la sala, le enviamos una oferta de video
        const offer = await peerConnectionRef.current?.createOffer({});
        if (offer) {
          await peerConnectionRef.current?.setLocalDescription(offer);
          socket.emit('offer', offer);
        }
      }
    });

    socket.on('offer', async (offer) => {
      if (mode === 'RECEIVER' && peerConnectionRef.current) {
        await peerConnectionRef.current.setRemoteDescription(new RTCSessionDescription(offer));
        const answer = await peerConnectionRef.current.createAnswer();
        await peerConnectionRef.current.setLocalDescription(answer);
        socket.emit('answer', answer);
      }
    });

    socket.on('answer', async (answer) => {
      if (mode === 'TRANSMITTER' && peerConnectionRef.current) {
        await peerConnectionRef.current.setRemoteDescription(new RTCSessionDescription(answer));
      }
    });

    socket.on('ice-candidate', async (candidate) => {
      if (peerConnectionRef.current) {
        await peerConnectionRef.current.addIceCandidate(new RTCIceCandidate(candidate));
      }
    });

    return () => {
      socket.off('user-joined');
      socket.off('offer');
      socket.off('answer');
      socket.off('ice-candidate');
    };
  }, [socket, mode]);

  const initWebRTC = () => {
    const peerConnection = new RTCPeerConnection(configuration);
    
    peerConnection.onicecandidate = (event) => {
      if (event.candidate && socket) {
        socket.emit('ice-candidate', event.candidate);
      }
    };

    peerConnection.ontrack = (event) => {
      if (event.streams && event.streams[0]) {
        setRemoteStream(event.streams[0]);
      }
    };

    peerConnectionRef.current = peerConnection;
    return peerConnection;
  };

  const handleStartTransmitter = async () => {
    if (!socket || !roomId) return;
    setMode('TRANSMITTER');
    
    const pc = initWebRTC();

    try {
      // Pedir permisos de grabación de pantalla
      const stream = await mediaDevices.getDisplayMedia({ video: true });
      localStreamRef.current = stream;

      stream.getTracks().forEach((track) => {
        pc.addTrack(track, stream);
      });

      socket.emit('join-room', roomId);
    } catch (error) {
      console.error('Error al capturar la pantalla:', error);
      setMode('HOME');
    }
  };

  const handleStartReceiver = () => {
    if (!socket || !roomId) return;
    setMode('RECEIVER');
    
    initWebRTC();
    socket.emit('join-room', roomId);
  };

  const handleStop = () => {
    if (localStreamRef.current) {
      localStreamRef.current.getTracks().forEach(track => track.stop());
    }
    if (peerConnectionRef.current) {
      peerConnectionRef.current.close();
    }
    setMode('HOME');
    setRemoteStream(null);
  };

  // --- VISTAS ---

  const renderHome = () => (
    <View style={styles.container}>
      <Text style={styles.title}>ASSET GUARDIAN</Text>
      <Text style={styles.subtitle}>Conexión Inalámbrica</Text>

      <View style={styles.card}>
        <Text style={styles.label}>Código de Sala de Vuelo</Text>
        <TextInput
          style={styles.input}
          value={roomId}
          onChangeText={setRoomId}
          placeholder="Ej: 1234"
          placeholderTextColor="#888"
          keyboardType="number-pad"
        />

        <TouchableOpacity style={[styles.button, styles.btnTransmit]} onPress={handleStartTransmitter}>
          <Text style={styles.buttonText}>📡 MODO TRANSMISOR</Text>
          <Text style={styles.buttonSubText}>(Capturar Pantalla del Dron)</Text>
        </TouchableOpacity>

        <TouchableOpacity style={[styles.button, styles.btnReceive]} onPress={handleStartReceiver}>
          <Text style={styles.buttonText}>💻 ESTACIÓN DE CONTROL</Text>
          <Text style={styles.buttonSubText}>(Recibir y Analizar Vuelo)</Text>
        </TouchableOpacity>
      </View>
      
      <Text style={styles.statusText}>
        Servidor: {isConnected ? '🟢 Conectado' : '🔴 Desconectado'}
      </Text>
    </View>
  );

  const renderTransmitter = () => (
    <View style={styles.container}>
      <Text style={styles.title}>📡 TRANSMISIÓN ACTIVA</Text>
      <View style={styles.card}>
        <Text style={styles.statusText}>Sala de vuelo: {roomId}</Text>
        <Text style={styles.statusText}>Capturando pantalla. El dron está en vivo.</Text>
        
        <TouchableOpacity style={[styles.button, { marginTop: 40, backgroundColor: '#d9534f' }]} onPress={handleStop}>
          <Text style={styles.buttonText}>DETENER VUELO</Text>
        </TouchableOpacity>
      </View>
    </View>
  );

  const renderReceiver = () => (
    <View style={styles.containerReceiver}>
      {remoteStream ? (
        <RTCView 
          streamURL={remoteStream.toURL()} 
          style={styles.rtcView} 
          objectFit="cover" 
        />
      ) : (
        <View style={styles.loadingView}>
          <Text style={styles.statusText}>Esperando recepción de video (Sala {roomId})...</Text>
        </View>
      )}

      {/* Aquí abajo integraríamos los botones de herramientas (Foto, Zoom, Análisis) */}
      <View style={styles.floatingToolbar}>
        <TouchableOpacity style={styles.toolBtn} onPress={handleStop}>
           <Text style={styles.buttonText}>⏹️ Finalizar</Text>
        </TouchableOpacity>
      </View>
    </View>
  );

  return (
    <SafeAreaView style={styles.root}>
      <StatusBar barStyle="light-content" backgroundColor="#0f172a" />
      {mode === 'HOME' && renderHome()}
      {mode === 'TRANSMITTER' && renderTransmitter()}
      {mode === 'RECEIVER' && renderReceiver()}
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  root: {
    flex: 1,
    backgroundColor: '#0f172a',
  },
  container: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    padding: 20,
  },
  containerReceiver: {
    flex: 1,
    backgroundColor: '#000',
  },
  rtcView: {
    flex: 1,
    width: '100%',
    height: '100%',
  },
  loadingView: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  title: {
    fontSize: 28,
    fontWeight: '900',
    color: '#38bdf8',
    marginBottom: 5,
    letterSpacing: 2,
  },
  subtitle: {
    fontSize: 16,
    color: '#94a3b8',
    marginBottom: 40,
  },
  card: {
    width: '100%',
    backgroundColor: 'rgba(30, 41, 59, 0.7)',
    borderRadius: 20,
    padding: 24,
    borderWidth: 1,
    borderColor: 'rgba(56, 189, 248, 0.2)',
  },
  label: {
    color: '#e2e8f0',
    fontSize: 14,
    marginBottom: 10,
    fontWeight: '600',
  },
  input: {
    backgroundColor: '#0f172a',
    borderRadius: 10,
    padding: 15,
    color: '#fff',
    fontSize: 18,
    borderWidth: 1,
    borderColor: '#334155',
    marginBottom: 30,
    textAlign: 'center',
    letterSpacing: 5,
  },
  button: {
    borderRadius: 12,
    padding: 18,
    alignItems: 'center',
    marginBottom: 15,
  },
  btnTransmit: {
    backgroundColor: '#0284c7',
  },
  btnReceive: {
    backgroundColor: '#10b981',
  },
  buttonText: {
    color: '#fff',
    fontSize: 16,
    fontWeight: 'bold',
  },
  buttonSubText: {
    color: 'rgba(255,255,255,0.7)',
    fontSize: 12,
    marginTop: 4,
  },
  statusText: {
    color: '#94a3b8',
    marginTop: 20,
    fontSize: 14,
  },
  floatingToolbar: {
    position: 'absolute',
    bottom: 30,
    left: 20,
    right: 20,
    flexDirection: 'row',
    justifyContent: 'space-around',
    backgroundColor: 'rgba(15, 23, 42, 0.8)',
    padding: 15,
    borderRadius: 15,
  },
  toolBtn: {
    backgroundColor: '#d9534f',
    padding: 10,
    borderRadius: 8,
  }
});

export default App;
