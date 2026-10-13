export interface ScannerDeviceDto {
  deviceId: string;
  name: string;
  manufacturer: string;
  description: string;
  isDefault: boolean;
  isSimulated: boolean;
}

export interface ScanRequest {
  deviceId?: string | null;
  resolutionDpi: number;
  colorMode: 'Color' | 'Greyscale' | 'BlackAndWhite';
  source: 'Feeder' | 'Flatbed';
  duplex: boolean;
}

export interface ScanResultDto {
  success: boolean;
  fileName: string;
  contentType: string;
  fileSize: number;
  base64Data: string;
  errorMessage?: string | null;
}
