<script setup>
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from '@/components/ui/dialog'
defineProps({ open: Boolean, title: String, description: { type: String, default: 'Uzupełnij dane i zapisz zmiany.' } })
defineEmits(['update:open'])
</script>
<template>
  <Dialog :open="open" @update:open="$emit('update:open', $event)">
    <DialogContent class="max-h-[90vh] overflow-y-auto">
      <DialogHeader><DialogTitle>{{ title }}</DialogTitle><DialogDescription>{{ description }}</DialogDescription></DialogHeader>
      <slot />
    </DialogContent>
  </Dialog>
</template>
