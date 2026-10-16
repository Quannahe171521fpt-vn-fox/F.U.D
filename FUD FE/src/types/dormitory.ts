export interface RoomType {
  id: string;
  name: string;
  capacity: number;
  area: string;
  pricePerSemester: string;
  features: string[];
  description: string;
  popular?: boolean;
}

export interface FaqItem {
  id: string;
  question: string;
  answer: string;
  category: 'dang-ky' | 'chi-phi' | 'noi-quy' | 'tien-ich';
}

export interface RegistrationFormData {
  fullName: string;
  studentId: string;
  email: string;
  phoneNumber: string;
  cohort: string;
  major: string;
  campus: string;
  roomType: string;
  preferredRoommateId: string;
  note: string;
}

export interface MaintenanceRequestData {
  studentName: string;
  studentId: string;
  building: string;
  roomNumber: string;
  issueCategory: string;
  description: string;
  urgency: 'low' | 'normal' | 'high';
}
