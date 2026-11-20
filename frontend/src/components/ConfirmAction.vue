<script setup>
import { AlertDialog, AlertDialogContent, AlertDialogHeader, AlertDialogTitle, AlertDialogDescription, AlertDialogFooter, AlertDialogCancel } from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
defineProps({ open: Boolean, text: String })
defineEmits(['update:open', 'confirm'])
</script>
<template>
  <AlertDialog :open="open" @update:open="$emit('update:open', $event)">
    <AlertDialogContent><AlertDialogHeader><AlertDialogTitle>Confirm action</AlertDialogTitle><AlertDialogDescription>{{ text }}</AlertDialogDescription></AlertDialogHeader>
      <AlertDialogFooter><AlertDialogCancel>Cancel</AlertDialogCancel><Button type="button" @click="$emit('confirm')">Confirm</Button></AlertDialogFooter>
    </AlertDialogContent>
  </AlertDialog>
</template>
