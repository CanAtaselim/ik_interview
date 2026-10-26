import { z } from 'zod';

export const loginSchema = z.object({
  username: z.string().trim().min(1, 'Kullanıcı adınızı girmelisiniz.'),
  password: z.string().min(1, 'Şifrenizi girmelisiniz.'),
});
