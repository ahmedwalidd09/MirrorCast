//
//  QRCodeScannerView.swift
//  MirrorCast
//
//  Camera QR code scanner using AVCaptureSession to scan Wi-Fi Direct credentials.
//

import SwiftUI
import AVFoundation

public struct QRCodeScannerView: View {
    @Environment(\.presentationMode) var presentationMode
    @ObservedObject var appState: AppState
    
    @State private var isFlashlightOn: Bool = false
    @State private var cameraError: String?
    
    public init(appState: AppState) {
        self.appState = appState
    }
    
    public var body: some View {
        ZStack {
            Color.black.ignoresSafeArea()
            
            #if targetEnvironment(simulator)
            VStack(spacing: 16) {
                Image(systemName: "camera.viewfinder")
                    .font(.system(size: 64))
                    .foregroundColor(.mcSilverMid)
                Text("Camera not available in iOS Simulator")
                    .font(MCTextStyle.body)
                    .foregroundColor(.mcSilverBright)
                
                Button(action: simulateDirectConnect) {
                    Text("Simulate Direct Connect (DIRECT-MC-TEST)")
                        .font(.system(size: 14, weight: .semibold))
                        .foregroundColor(.black)
                        .padding(.horizontal, 16)
                        .padding(.vertical, 10)
                        .background(Color.mcSilverBright)
                        .cornerRadius(8)
                }
            }
            .padding()
            #else
            CameraScannerRepresentable(
                onCodeScanned: handleScannedCode,
                onError: { err in cameraError = err }
            )
            .ignoresSafeArea()
            #endif
            
            // Reticle and UI Overlays
            VStack {
                // Header Bar
                HStack {
                    Button(action: { presentationMode.wrappedValue.dismiss() }) {
                        Image(systemName: "xmark")
                            .font(.system(size: 16, weight: .semibold))
                            .foregroundColor(.mcSilverBright)
                            .frame(width: 40, height: 40)
                            .background(Color.black.opacity(0.6))
                            .clipShape(Circle())
                    }
                    
                    Spacer()
                    
                    Text("Scan Receiver QR")
                        .font(MCTextStyle.headline)
                        .foregroundColor(.mcSilverBright)
                    
                    Spacer()
                    
                    Button(action: toggleTorch) {
                        Image(systemName: isFlashlightOn ? "bolt.fill" : "bolt.slash.fill")
                            .font(.system(size: 16, weight: .semibold))
                            .foregroundColor(isFlashlightOn ? .yellow : .mcSilverBright)
                            .frame(width: 40, height: 40)
                            .background(Color.black.opacity(0.6))
                            .clipShape(Circle())
                    }
                }
                .padding(.horizontal, 20)
                .padding(.top, 16)
                
                Spacer()
                
                // Target Reticle
                ZStack {
                    RoundedRectangle(cornerRadius: 16)
                        .stroke(Color.mcSilverBright.opacity(0.3), lineWidth: 2)
                        .frame(width: 250, height: 250)
                    
                    // Reticle Corner Accents
                    ReticleCornersShape()
                        .stroke(Color.mcSilverBright, lineWidth: 4)
                        .frame(width: 250, height: 250)
                }
                
                Spacer()
                
                // Bottom helper instructions
                VStack(spacing: 8) {
                    Text("Align the QR code within the frame")
                        .font(MCTextStyle.body)
                        .foregroundColor(.mcSilverBright)
                    
                    Text("Windows Receiver: Click 'Show QR Code' in Wi-Fi Direct mode")
                        .font(MCTextStyle.footnote)
                        .foregroundColor(.mcSilverMid)
                        .multilineTextAlignment(.center)
                }
                .padding(.horizontal, 30)
                .padding(.bottom, 40)
            }
        }
    }
    
    private func handleScannedCode(_ code: String) {
        guard let credentials = HotspotManager.parseWiFiQRCode(code) else {
            cameraError = "Unrecognized QR code format. Expected WIFI:S:DIRECT-MC-...;"
            return
        }
        
        let generator = UINotificationFeedbackGenerator()
        generator.notificationOccurred(.success)
        
        presentationMode.wrappedValue.dismiss()
        appState.connectToHotspot(credentials: credentials)
    }
    
    private func simulateDirectConnect() {
        let creds = WiFiCredentials(ssid: "DIRECT-MC-DESKTOP", password: "MirrorCastPassword123", securityType: "WPA")
        presentationMode.wrappedValue.dismiss()
        appState.connectToHotspot(credentials: creds)
    }
    
    private func toggleTorch() {
        guard let device = AVCaptureDevice.default(for: .video), device.hasTorch else { return }
        do {
            try device.lockForConfiguration()
            device.torchMode = isFlashlightOn ? .off : .on
            isFlashlightOn.toggle()
            device.unlockForConfiguration()
        } catch {
            print("Failed to toggle torch: \(error)")
        }
    }
}

// MARK: - Reticle Corner Accents Shape

struct ReticleCornersShape: Shape {
    let cornerLength: CGFloat = 28
    let radius: CGFloat = 16
    
    func path(in rect: CGRect) -> Path {
        var path = Path()
        
        // Top Left
        path.move(to: CGPoint(x: rect.minX, y: rect.minY + cornerLength))
        path.addLine(to: CGPoint(x: rect.minX, y: rect.minY + radius))
        path.addQuadCurve(to: CGPoint(x: rect.minX + radius, y: rect.minY), control: CGPoint(x: rect.minX, y: rect.minY))
        path.addLine(to: CGPoint(x: rect.minX + cornerLength, y: rect.minY))
        
        // Top Right
        path.move(to: CGPoint(x: rect.maxX - cornerLength, y: rect.minY))
        path.addLine(to: CGPoint(x: rect.maxX - radius, y: rect.minY))
        path.addQuadCurve(to: CGPoint(x: rect.maxX, y: rect.minY + radius), control: CGPoint(x: rect.maxX, y: rect.minY))
        path.addLine(to: CGPoint(x: rect.maxX, y: rect.minY + cornerLength))
        
        // Bottom Right
        path.move(to: CGPoint(x: rect.maxX, y: rect.maxY - cornerLength))
        path.addLine(to: CGPoint(x: rect.maxX, y: rect.maxY - radius))
        path.addQuadCurve(to: CGPoint(x: rect.maxX - radius, y: rect.maxY), control: CGPoint(x: rect.maxX, y: rect.maxY))
        path.addLine(to: CGPoint(x: rect.maxX - cornerLength, y: rect.maxY))
        
        // Bottom Left
        path.move(to: CGPoint(x: rect.minX + cornerLength, y: rect.maxY))
        path.addLine(to: CGPoint(x: rect.minX + radius, y: rect.maxY))
        path.addQuadCurve(to: CGPoint(x: rect.minX, y: rect.maxY - radius), control: CGPoint(x: rect.minX, y: rect.maxY))
        path.addLine(to: CGPoint(x: rect.minX, y: rect.maxY - cornerLength))
        
        return path
    }
}

// MARK: - Camera Scanner Representable

struct CameraScannerRepresentable: UIViewControllerRepresentable {
    let onCodeScanned: (String) -> Void
    let onError: (String) -> Void
    
    func makeUIViewController(context: Context) -> CameraScannerViewController {
        let controller = CameraScannerViewController()
        controller.delegate = context.coordinator
        return controller
    }
    
    func updateUIViewController(_ uiViewController: CameraScannerViewController, context: Context) {}
    
    func makeCoordinator() -> Coordinator {
        Coordinator(onCodeScanned: onCodeScanned, onError: onError)
    }
    
    class Coordinator: NSObject, AVCaptureMetadataOutputObjectsDelegate {
        let onCodeScanned: (String) -> Void
        let onError: (String) -> Void
        private var hasScanned = false
        
        init(onCodeScanned: @escaping (String) -> Void, onError: @escaping (String) -> Void) {
            self.onCodeScanned = onCodeScanned
            self.onError = onError
        }
        
        func metadataOutput(_ output: AVCaptureMetadataOutput, didOutput metadataObjects: [AVMetadataObject], from connection: AVCaptureConnection) {
            guard !hasScanned,
                  let metadata = metadataObjects.first as? AVMetadataMachineReadableCodeObject,
                  let stringValue = metadata.stringValue else {
                return
            }
            hasScanned = true
            DispatchQueue.main.async {
                self.onCodeScanned(stringValue)
            }
        }
    }
}

class CameraScannerViewController: UIViewController {
    weak var delegate: AVCaptureMetadataOutputObjectsDelegate?
    private var captureSession: AVCaptureSession?
    private var previewLayer: AVCaptureVideoPreviewLayer?
    
    override func viewDidLoad() {
        super.viewDidLoad()
        setupCaptureSession()
    }
    
    override func viewDidLayoutSubviews() {
        super.viewDidLayoutSubviews()
        previewLayer?.frame = view.bounds
    }
    
    override func viewWillAppear(_ animated: Bool) {
        super.viewWillAppear(animated)
        if captureSession?.isRunning == false {
            DispatchQueue.global(qos: .userInitiated).async { [weak self] in
                self?.captureSession?.startRunning()
            }
        }
    }
    
    override func viewWillDisappear(_ animated: Bool) {
        super.viewWillDisappear(animated)
        if captureSession?.isRunning == true {
            DispatchQueue.global(qos: .userInitiated).async { [weak self] in
                self?.captureSession?.stopRunning()
            }
        }
    }
    
    private func setupCaptureSession() {
        let session = AVCaptureSession()
        guard let device = AVCaptureDevice.default(for: .video),
              let input = try? AVCaptureDeviceInput(device: device) else {
            return
        }
        
        if session.canAddInput(input) {
            session.addInput(input)
        }
        
        let output = AVCaptureMetadataOutput()
        if session.canAddOutput(output) {
            session.addOutput(output)
            output.setMetadataObjectsDelegate(delegate, queue: DispatchQueue.main)
            output.metadataObjectTypes = [.qr]
        }
        
        let preview = AVCaptureVideoPreviewLayer(session: session)
        preview.videoGravity = .resizeAspectFill
        view.layer.addSublayer(preview)
        
        self.captureSession = session
        self.previewLayer = preview
    }
}
