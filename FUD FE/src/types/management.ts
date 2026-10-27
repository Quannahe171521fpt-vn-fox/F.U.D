export interface NewsItem {
  id: number;
  title: string;
  content: string;
  status: 'Published' | 'Draft';
  publishedDate: string | null; // formatted as dd/MM/yyyy or null for draft
  attachments?: {
    name: string;
    size: string;
    type: string;
  }[];
}

export interface EquipmentRecord {
  id: number;
  assetCode: string;
  name: string;
  category: string;
  location: string; // Room or Bed assigned to
  condition: 'Good' | 'Under repair' | 'Broken'; // Tốt, Đang sửa, Hỏng
  hasOpenTicket?: boolean; // If linked to an open incident ticket
}

export interface EquipmentCatalogItem {
  id: number;
  category: string;
  name: string;
  inUseCount: number;
}
