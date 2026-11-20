import { it, expect, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import LoginView from './LoginView.vue'
const mocks = vi.hoisted(() => ({ api: vi.fn(), replace: vi.fn(), check: vi.fn() }))
vi.mock('@/api/client', () => ({ api: mocks.api }))
vi.mock('@/composables/workspace', () => ({ checkSession: mocks.check }))
vi.mock('vue-router', () => ({ useRouter: () => ({ replace: mocks.replace }), useRoute: () => ({ query: {} }) }))
it('keeps the login form and does not navigate after a network failure', async () => {
  mocks.api.mockRejectedValue(new TypeError('Unable to connect to the server'))
  const wrapper = mount(LoginView)
  await wrapper.find('#email').setValue('user@example.com'); await wrapper.find('#password').setValue('Password-42!')
  await wrapper.find('form').trigger('submit'); await flushPromises()
  expect(wrapper.get('[role="alert"]').text()).toContain('Unable to connect')
  expect(mocks.replace).not.toHaveBeenCalled(); wrapper.unmount()
})
