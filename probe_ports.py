import socket
import sys

def probe_port(port):
    try:
        s = socket.create_connection(('localhost', port), timeout=1)
        print(f"Connected to {port}")
        # Identify by reading banner
        try:
             banner = s.recv(1024)
             print(f"Port {port} Banner: {banner}")
        except socket.timeout:
             print(f"Port {port} Timeout reading banner")
        s.close()
    except Exception as e:
        print(f"Port {port} closed/error: {e}")

ports = [34999, 55504, 56069, 63032, 38000]
for p in ports:
    probe_port(p)
