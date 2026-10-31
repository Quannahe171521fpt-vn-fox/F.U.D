export interface NewsItem {
  id: number;
  title: string;
  content: string;
  status: 'Published' | 'Draft' | 'Archived';
  publishedDate: string | null; // formatted as dd/MM/yyyy or null for draft
  createdDate?: string | null;
  hiddenDate?: string | null;
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


export interface TermLookupItem {
  id: number;
  name: string;
  academicYear: string;
  displayName: string;
  startDate: string;
  endDate: string;
  isRegistrationStarted: boolean;
  registrationStatus: string;
}

export interface PricingItem {
  dormId: number;
  dormName: string;
  dormCode: string;
  roomTypeId: number;
  roomTypeName: string;
  bedCapacity: number;
  bedPosition: string;
  previousSemesterPrice: number | null;
  currentPrice: number;
  unit: string;
}

export interface TermPricingData {
  termId: number;
  termName: string;
  academicYear: string;
  status: string;
  isRegistrationStarted: boolean;
  registrationStatus: string;
  publishedTime: string | null;
  items: PricingItem[];
}

export interface SavePricingPayload {
  termId: number;
  items: {
    dormId: number;
    roomTypeId: number;
    currentPrice: number;
  }[];
  confirmApplyToNewBookingsOnly?: boolean;
}